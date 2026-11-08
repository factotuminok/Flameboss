namespace Flameboss;

public class FlameBossPollingService: BackgroundService
{
    private readonly MqttService _mqtt;
    private readonly CookReadingStore _readings;
    private readonly FlameBossService _flameBossService;
    private readonly ILogger<FlameBossPollingService> _logger;
    private readonly Configuration _config;
    private int millisecondsDelay = 5000;

    public FlameBossPollingService(MqttService mqtt, CookReadingStore readings, FlameBossService flameBossService, Configuration config, ILogger<FlameBossPollingService> logger)
    {
        _config = config;
        _mqtt = mqtt;
        _readings = readings;
        _flameBossService = flameBossService;
        _logger = logger;
        millisecondsDelay = config.PollSeconds * 1000;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Captured before the read so a reading is never filed under the next cook (or none) if the cook is toggled mid-poll.
            var cookId = FlamebossData.CookId;
            if (FlamebossData.Cooking && cookId is not null)
            {
                FlameBossStatus? temps = null;
                try
                {
                    temps = _flameBossService.GetStatus().Result;
                    await _mqtt.UpdateAndPublish(temps);

                    _logger.LogDebug("Updated → Pit: {Pit}°F, Meat1: {Meat1}°F, Blower {blower}%, SetTemp {setTemp}°F ",
                        temps.Pit, temps.Meat1, temps.BlowerPercentage, temps.SetTemperature);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch or parse Flame Boss data");
                }

                // Only queues; CookReadingStore writes to Mongo in the background so an outage can't stall polling.
                if (temps is not null)
                    _readings.Add(cookId, temps);
            }
            FlamebossData.LastPoll = DateTime.Now;
            await Task.Delay(millisecondsDelay, stoppingToken); 
        }
    }
}