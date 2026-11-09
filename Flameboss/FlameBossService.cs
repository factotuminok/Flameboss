using System.Text.RegularExpressions;
namespace Flameboss;

public class FlameBossService
{
    
    private readonly HttpClient _httpClient;
    private readonly string _flameBossUrl;
    private readonly ILogger<FlameBossService> _logger;
    private readonly MqttService _mqtt;
    
    public FlameBossService(IHttpClientFactory httpClientFactory, MqttService mqtt, Configuration config, ILogger<FlameBossService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        // The default 100 s would let a powered-off controller stall each poll; a LAN page answers well within this.
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _flameBossUrl = config.FlameBossUrl; // Default IP
        _logger = logger;
        _mqtt = mqtt;
    }
    
    public async Task<FlameBossStatus> GetStatus()
    {
        string statushtml = await _httpClient.GetStringAsync(_flameBossUrl);
        string sethtml = await _httpClient.GetStringAsync(_flameBossUrl + "set");
        // Any missing field throws, so a page that doesn't parse counts as a failed read (not logged, counts
        // toward the unreachable auto-stop) instead of a reading of zeros. "---" (probe unplugged) is a real 0.
        FlamebossData.CurrentStatus = new FlameBossStatus
        {
            Pit = ExtractNumber(statushtml, @"<td>\s*Pit\s*</td>\s*<td[^>]*>\s*(\d+|---)", "Pit"),
            Meat1 = ExtractNumber(statushtml, @"<td>\s*Meat\s+1\s*</td>\s*<td[^>]*>\s*(\d+|---)", "Meat 1"),
            SetTemperature = ExtractSetTemperature(sethtml),
            BlowerPercentage = ExtractNumber(statushtml, @"<td>\s*Blower\s*</td>\s*<td[^>]*>\s*(\d+|---)", "Blower"),
            LastUpdate = DateTime.Now
        };
        FlamebossData.LastSuccessfulCheck = DateTime.Now;
        if (FlamebossData.Cooking)
        {
            FlamebossData.CookStatusList.Add(FlamebossData.CurrentStatus);
        }

        return FlamebossData.CurrentStatus;

    }
    
    private static int ExtractSetTemperature(string htmlContent)
    {
        var match = Regex.Match(htmlContent, @"name=[""']s[""']\s+[^>]*value=[""'](\d*)[""']", RegexOptions.IgnoreCase);
        if (!match.Success)
            throw new InvalidDataException("Set temperature field not found on the controller's /set page");

        return int.TryParse(match.Groups[1].Value, out int temp) ? temp : 0;
    }

    private static int ExtractNumber(string html, string pattern, string field)
    {
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
            throw new InvalidDataException($"{field} not found on the controller's status page");

        string val = match.Groups[1].Value.Trim();
        return val == "---" ? 0 : int.Parse(val);
    }
    
    public async Task<SetTemperatureResponse> SetTemperature(int temperature)
    {
        // Construct the POST request to Flame Boss
        var formData = new Dictionary<string, string>
        {
            { "s", temperature.ToString() }
        };

        var formContent = new FormUrlEncodedContent(formData);
        var response = await _httpClient.PostAsync($"{_flameBossUrl}set", formContent);

        if (response.IsSuccessStatusCode)
        {
            return new SetTemperatureResponse
            {
                Success = true,
                NewTemperature = temperature.ToString(),
                Message = $"Temperature set to {temperature}°F"
            };
        }
        else
        {
            throw new Exception($"Failed to set temperature. HTTP {response.StatusCode}");
        }

    }

   
    public async Task<bool> StartCook()
    {
        FlamebossData.CookStatusList = new List<FlameBossStatus>();
        FlamebossData.CookId = Guid.NewGuid().ToString();
        FlamebossData.Cooking =  true;
        await GetStatus();
        _logger.LogInformation("Cooking started, cook {CookId}", FlamebossData.CookId);
        return FlamebossData.Cooking;
    }
    
    public async Task<bool> StopCook()
    {
        FlamebossData.CookStatusList.Clear();
        FlamebossData.Cooking =  false;
        FlamebossData.CookId = null;
        _logger.LogInformation("Cooking stopped");
        try
        {
            await _mqtt.PublishCookStopped();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish zeroed Flame Boss data to MQTT after stopping the cook");
        }
        return FlamebossData.Cooking;
    }

    /// <summary>Stops the cook if one is running, otherwise starts one. Returns whether a cook is now running.</summary>
    public async Task<bool> ToggleCook()
    {
        return FlamebossData.Cooking ? await StopCook() : await StartCook();
    }
}