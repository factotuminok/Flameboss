using System.Text.RegularExpressions;

namespace Flameboss;

public class FlameBossPollingService: BackgroundService
{
    private readonly MqttService _mqtt;
    private readonly FlameBossService _flameBossService;
    private readonly ILogger<FlameBossPollingService> _logger;
    private readonly Configuration _config;
    private int millisecondsDelay = 5000;

    public FlameBossPollingService(MqttService mqtt, FlameBossService flameBossService, Configuration config, ILogger<FlameBossPollingService> logger)
    {
        _config = config;
        _mqtt = mqtt;
        _flameBossService = flameBossService;
        _logger = logger;
        if(config.PollSeconds != null)
            millisecondsDelay = Convert.ToInt32(config.PollSeconds) * 1000;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (FlamebossData.Cooking)
            {
                try
                {
                    var temps = _flameBossService.GetStatus().Result;
                    await _mqtt.UpdateAndPublish(temps);

                    _logger.LogDebug("Updated → Pit: {Pit}°F, Meat1: {Meat1}°F, Blower {blower}%, SetTemp {setTemp}°F ",
                        temps.Pit, temps.Meat1, temps.BlowerPercentage, temps.SetTemperature);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch or parse Flame Boss data");
                }
            }
            FlamebossData.LastPoll = DateTime.Now;
            await Task.Delay(millisecondsDelay, stoppingToken); 
        }
    }
}