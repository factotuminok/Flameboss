using System.Text.RegularExpressions;
namespace Flameboss;

public class FlameBossService
{
    
    private readonly HttpClient _httpClient;
    private readonly string _flameBossUrl;
    private readonly ILogger<FlameBossService> _logger;
    
    public FlameBossService(IHttpClientFactory httpClientFactory, Configuration config, ILogger<FlameBossService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _flameBossUrl = config.FlameBossUrl; // Default IP
        _logger = logger;
    }
    
    public async Task<FlameBossStatus> GetStatus()
    {
        string statushtml = await _httpClient.GetStringAsync(_flameBossUrl);
        string sethtml = await _httpClient.GetStringAsync(_flameBossUrl + "set");
        FlamebossData.CurrentStatus = new FlameBossStatus
        {
            Pit = ExtractNumber(statushtml, @"<td>\s*Pit\s*</td>\s*<td[^>]*>\s*(\d+)"),
            Meat1 = ExtractNumber(statushtml, @"<td>\s*Meat\s+1\s*</td>\s*<td[^>]*>\s*(\d+|---)"),
            SetTemperature = ExtractSetTemperature(sethtml),
            BlowerPercentage = ExtractNumber(statushtml, @"<td>\s*Blower\s*</td>\s*<td[^>]*>\s*(\d+|---)"),
            LastUpdate = DateTime.Now
        };
        FlamebossData.LastSuccessfulCheck = DateTime.Now;
        if (FlamebossData.Cooking)
        {
            FlamebossData.CookStatusList.Add(FlamebossData.CurrentStatus);
        }

        return FlamebossData.CurrentStatus;

    }
    
    private int ExtractSetTemperature(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent)) 
            return 0;
        
        string pattern = @"name=[""']s[""']\s+[^>]*value=[""'](\d+)[""']";
        var match = Regex.Match(htmlContent, pattern, 
            RegexOptions.IgnoreCase);
        
        return match.Success && int.TryParse(match.Groups[1].Value, out int temp) ? temp : 0;
    }
    
    private int ExtractNumber(string html, string pattern)
    {
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        if (!match.Success) 
            return 0;

        string val = match.Groups[1].Value.Trim();
        return val == "---" ? 0 : int.TryParse(val, out int n) ? n : 0;

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
        FlamebossData.Cooking =  true;
        await GetStatus();
        _logger.LogInformation("Cooking started");
        return FlamebossData.Cooking;
    }
    
    public bool StopCook()
    {
        FlamebossData.CookStatusList.Clear();
        FlamebossData.Cooking =  false;
        _logger.LogInformation("Cooking stopped");
        return FlamebossData.Cooking;
    }
}