namespace Flameboss;

/// <summary>
/// Every AUTOSTARTCHECKSECONDS, while no cook is running, checks whether the controller is on (a successful,
/// parseable read) and starts a cook if it is. Independent of the cook poll in <see cref="FlameBossPollingService"/>.
/// <para>
/// Armed at app start and whenever a check finds the controller unreachable; disarmed once a cook is running.
/// So a cook resumes after a restart or a power-on, but a cook stopped by hand stays stopped while the controller
/// is still on (cool-down), until the controller has been off.
/// </para>
/// </summary>
public class CookAutoStartService : BackgroundService
{
    private readonly FlameBossService _flameBoss;
    private readonly ILogger<CookAutoStartService> _logger;
    private readonly TimeSpan _interval;
    private bool _armed = true;

    public CookAutoStartService(FlameBossService flameBoss, Configuration config, ILogger<CookAutoStartService> logger)
    {
        _flameBoss = flameBoss;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(config.AutoStartCheckSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_interval == TimeSpan.Zero)
        {
            _logger.LogInformation("Cook auto-start is disabled (AUTOSTARTCHECKSECONDS=0)");
            return;
        }

        // First check right away, so a cook that was running before a restart resumes without waiting a full interval.
        do
        {
            await CheckAsync();
        } while (await WaitAsync(stoppingToken));
    }

    private async Task<bool> WaitAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(_interval, stoppingToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task CheckAsync()
    {
        if (FlamebossData.Cooking)
        {
            _armed = false;
            return;
        }

        try
        {
            await _flameBoss.GetStatus();
        }
        catch (Exception ex)
        {
            if (!_armed)
                _logger.LogInformation("Controller not reachable ({Error}); cook auto-start armed", ex.Message);
            _armed = true;
            return;
        }

        if (!_armed)
            return; // On, but the last cook was stopped while it stayed on: wait for it to be turned off first.

        _armed = false;
        if (FlamebossData.Cooking)
            return; // Started by hand while we were reading.

        try
        {
            await _flameBoss.StartCook();
            _logger.LogInformation("Controller is on: auto-started cook {CookId}", FlamebossData.CookId);
        }
        catch (Exception ex)
        {
            // StartCook marks the cook running before its first read, so the cook poll and the unreachable
            // auto-stop take over from here; an exception must not escape and stop the host.
            _logger.LogWarning(ex, "Auto-started cook {CookId}, but its first read failed", FlamebossData.CookId);
        }
    }
}
