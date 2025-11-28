using Flameboss;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/")]
public class FlameBossController : ControllerBase
{
    private readonly Configuration _config;
    private readonly FlameBossService _flameBossService;
    private readonly ILogger<FlameBossController> _logger;
    
    public FlameBossController(Configuration config, FlameBossService flameBossService, ILogger<FlameBossController> logger)
    {
        _config = config;
        _flameBossService = flameBossService;
        _logger = logger;
    }

    /// <summary>
    /// Set the target temperature on Flame Boss controller
    /// </summary>
    /// <param name="temperature">Target temperature (Fahrenheit)</param>
    /// <returns>Success response with current status</returns>
    [HttpPost("set-temperature")]
    public async Task<IActionResult> SetTemperature([FromQuery] int temperature)
    {
        if (_flameBossService.Cooking == false)
            return BadRequest($"Flame Boss not cooking");
        if (temperature < 100 || temperature > 400)
            return BadRequest("Temperature must be between 100°F and 400°F");
        if(temperature.ToString() == _flameBossService.CurrentStatus.SetTemperature)
            return Ok($"Temperature already set to {temperature}");

        try
        {
            var result = _flameBossService.SetTemperature(temperature);

            if(result.Result.Success)
            {
                return Ok(new SetTemperatureResponse
                {
                    Success = true,
                    NewTemperature = result.Result.NewTemperature,
                    Message = $"Temperature set to {temperature}°F"
                });
            }
            else
            {
                return StatusCode(500, "Internal error");
            }

        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, $"Flame Boss unreachable: {ex.Message}");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get current Flame Boss status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            if (_flameBossService.Cooking && _flameBossService.CurrentStatus != null)
            {
                var temps = _flameBossService.CurrentStatus;
                return Ok(temps);
            }
            else
                return BadRequest($"Flame Boss not cooking");
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, $"Cannot reach Flame Boss: {ex.Message}");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// Start Cook
    /// </summary>
    [HttpGet("toggle-cook")]
    public async Task<IActionResult> ToggleCook()
    {
        if (_flameBossService.Cooking)
        {
            _flameBossService.Cooking = false;
            _logger.LogInformation($"Cook stopped");
            return Ok($"Cook stopped");
        }
        else
        {
            _flameBossService.Cooking = true;
            await _flameBossService.GetStatus();
            _logger.LogInformation($"Cook started");
            return Ok($"Cook started");
        }
    }
    
    /// <summary>
    /// Get ready status
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        return Ok("Heartbeat");
    }

    /// <summary>
    /// Get alive status
    /// </summary>
    [HttpGet("alive")]
    public async Task<IActionResult> Alive()
    {
        int pollSeconds = Convert.ToInt32(_config.PollSeconds) * 5;
        if (DateTime.Now.AddSeconds(-pollSeconds) <= _flameBossService.LastPoll)
            if (_flameBossService.Cooking)
                if (DateTime.Now.AddSeconds(-pollSeconds) <= _flameBossService.LastCheck)
                    return Ok("Heartbeat");
                else
                    return BadRequest("No Heartbeat");
            else
                return Ok("Heartbeat");
        else
            return BadRequest("No Heartbeat");
            
    }
}