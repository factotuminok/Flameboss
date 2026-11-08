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

/// <summary>One cook's span and peaks, aggregated from its readings.</summary>
public class CookSummary
{
    public required string CookId { get; init; }
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public int MaxPit { get; init; }
    public int MaxMeat { get; init; }
    public int Readings { get; init; }
    public TimeSpan Duration => End - Start;
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

    /// <summary>False when MONGODBURI is unset, so there is no cook history to show.</summary>
    public bool IsEnabled => _collection is not null;

    /// <summary>Every logged cook, newest first. Start/End are UTC (first and last reading).</summary>
    public async Task<List<CookSummary>> GetCookSummariesAsync(CancellationToken cancellationToken = default)
    {
        if (_collection is null)
            return [];

        return await _collection.Aggregate()
            .Group(r => r.CookId, g => new CookSummary
            {
                CookId = g.Key,
                Start = g.Min(r => r.Timestamp),
                End = g.Max(r => r.Timestamp),
                MaxPit = g.Max(r => r.Pit),
                MaxMeat = g.Max(r => r.Meat1),
                Readings = g.Count(),
            })
            .SortByDescending(s => s.Start)
            .ToListAsync(cancellationToken);
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
