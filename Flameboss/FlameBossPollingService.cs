namespace Flameboss;

public class FlameBossPollingService: BackgroundService
{
    private readonly MqttService _mqtt;
    private readonly CookReadingStore _readings;
    private readonly FlameBossService _flameBossService;
    private readonly ILogger<FlameBossPollingService> _logger;
    private readonly Configuration _config;
    private int millisecondsDelay = 5000;
    private readonly TimeSpan _unreachableTimeout;

    // Last successful controller read for the cook being tracked; reset when a new cook starts.
    private string? _trackedCookId;
    private DateTime _lastReachable;

    public FlameBossPollingService(MqttService mqtt, CookReadingStore readings, FlameBossService flameBossService, Configuration config, ILogger<FlameBossPollingService> logger)
    {
        _config = config;
        _mqtt = mqtt;
        _readings = readings;
        _flameBossService = flameBossService;
        _logger = logger;
        millisecondsDelay = config.PollSeconds * 1000;
        _unreachableTimeout = TimeSpan.FromSeconds(config.UnreachableTimeoutSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Captured before the read so a reading is never filed under the next cook (or none) if the cook is toggled mid-poll.
            var cookId = FlamebossData.CookId;
            if (FlamebossData.Cooking && cookId is not null)
            {
                if (cookId != _trackedCookId)
                {
                    _trackedCookId = cookId;
                    _lastReachable = DateTime.UtcNow;
                }

                FlameBossStatus? temps = null;
                try
                {
                    temps = await _flameBossService.GetStatus();
                    _lastReachable = DateTime.UtcNow;

                    _logger.LogDebug("Updated → Pit: {Pit}°F, Meat1: {Meat1}°F, Blower {blower}%, SetTemp {setTemp}°F ",
                        temps.Pit, temps.Meat1, temps.BlowerPercentage, temps.SetTemperature);
                }
                catch (Exception ex)
                {
                    var unreachableFor = DateTime.UtcNow - _lastReachable;
                    _logger.LogError(ex, "Failed to read the Flame Boss controller (unreachable for {Seconds:F0}s)", unreachableFor.TotalSeconds);

                    // Only stop the cook this poll belongs to; a cook started meanwhile has its own clock.
                    if (unreachableFor >= _unreachableTimeout && FlamebossData.CookId == cookId)
                    {
                        await _flameBossService.StopCook();
                        _logger.LogWarning("Stopped cook {CookId}: controller unreachable for {Seconds:F0}s (limit {Limit}s)",
                            cookId, unreachableFor.TotalSeconds, _unreachableTimeout.TotalSeconds);
                    }
                }

                if (temps is not null)
                {
                    try
                    {
                        await _mqtt.UpdateAndPublish(temps, cookId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to publish Flame Boss data to MQTT");
                    }

                    // Only queues; CookReadingStore writes to Mongo in the background so an outage can't stall polling.
                    _readings.Add(cookId, temps);
                }
            }
            FlamebossData.LastPoll = DateTime.Now;
            await Task.Delay(millisecondsDelay, stoppingToken); 
        }
    }
}