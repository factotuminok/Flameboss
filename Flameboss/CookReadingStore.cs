using System.Threading.Channels;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Flameboss;

/// <summary>One poll of the controller during a cook, stored in the <c>readings</c> collection.</summary>
public class CookReading
{
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("cookId")] public required string CookId { get; init; }
    [BsonElement("timestamp")] public DateTime Timestamp { get; init; }
    [BsonElement("pit")] public int Pit { get; init; }
    [BsonElement("meat1")] public int Meat1 { get; init; }
    [BsonElement("setTemperature")] public int SetTemperature { get; init; }
    [BsonElement("blowerPercentage")] public int BlowerPercentage { get; init; }
}

/// <summary>
/// Logs cook readings to MongoDB. <see cref="Add"/> only queues, so a slow or down Mongo never stalls polling
/// or MQTT; the background loop writes in order and retries until Mongo is back. Disabled (warned once at
/// startup) when MONGODBURI is unset.
/// </summary>
public class CookReadingStore : BackgroundService
{
    public const string CollectionName = "readings";

    // ~14 h of readings at the default 5 s poll; past that the oldest unwritten reading is dropped.
    private const int QueueCapacity = 10_000;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly IMongoCollection<CookReading>? _collection;
    private readonly ILogger<CookReadingStore> _logger;
    private readonly Channel<CookReading> _queue = Channel.CreateBounded<CookReading>(
        new BoundedChannelOptions(QueueCapacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public CookReadingStore(Configuration config, ILogger<CookReadingStore> logger)
    {
        _logger = logger;
        if (string.IsNullOrWhiteSpace(config.MongoDbUri))
        {
            logger.LogWarning("MONGODBURI is not set: cook readings won't be logged to MongoDB");
            return;
        }

        var settings = MongoClientSettings.FromConnectionString(config.MongoDbUri);
        // Fail fast so a hung connection turns into a retry instead of blocking the writer indefinitely.
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        settings.SocketTimeout = TimeSpan.FromSeconds(10);
        _collection = new MongoClient(settings)
            .GetDatabase(config.MongoDbDatabase)
            .GetCollection<CookReading>(CollectionName);
    }

    public void Add(string cookId, FlameBossStatus status)
    {
        if (_collection is null)
            return;

        _queue.Writer.TryWrite(new CookReading
        {
            CookId = cookId,
            Timestamp = status.LastUpdate.ToUniversalTime(),
            Pit = status.Pit,
            Meat1 = status.Meat1,
            SetTemperature = status.SetTemperature,
            BlowerPercentage = status.BlowerPercentage,
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_collection is null)
            return;

        var indexed = false;
        await foreach (var reading in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            while (true)
            {
                try
                {
                    if (!indexed)
                    {
                        await _collection.Indexes.CreateOneAsync(
                            new CreateIndexModel<CookReading>(Builders<CookReading>.IndexKeys
                                .Ascending(r => r.CookId)
                                .Ascending(r => r.Timestamp)),
                            cancellationToken: stoppingToken);
                        indexed = true;
                    }

                    await _collection.InsertOneAsync(reading, cancellationToken: stoppingToken);
                    break;
                }
                catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
                {
                    // The driver assigned _id on the first attempt; an earlier try was written but its reply was lost.
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to log reading for cook {CookId} to MongoDB; retrying in {Delay}s ({Queued} queued)",
                        reading.CookId, RetryDelay.TotalSeconds, _queue.Reader.Count + 1);
                    await Task.Delay(RetryDelay, stoppingToken);
                }
            }
        }
    }
}
