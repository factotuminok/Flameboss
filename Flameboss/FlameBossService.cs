using System.Text.RegularExpressions;
namespace Flameboss;

public class FlameBossService
{
    
    private readonly HttpClient _httpClient;
    private readonly string _flameBossUrl;
    private readonly Configuration _config;
    private readonly ILogger<FlameBossService> _logger;
    public FlameBossStatus? CurrentStatus { get; set; }
    public bool Cooking { get; set; }
    public DateTime LastCheck { get; set; }
    public DateTime LastPoll { get; set; }
    
    public FlameBossService(IHttpClientFactory httpClientFactory, Configuration config, ILogger<FlameBossService> logger)
    {
        _config = config;
        _httpClient = httpClientFactory.CreateClient();
        _flameBossUrl = config.FlameBossUrl; // Default IP
        _logger = logger;
    }
    
    public async Task<FlameBossStatus> GetStatus()
    {
        string statushtml = await _httpClient.GetStringAsync(_flameBossUrl);
        string sethtml = await _httpClient.GetStringAsync(_flameBossUrl + "set");
        CurrentStatus = new FlameBossStatus
        {
            Pit = ExtractNumber(statushtml, @"<td>\s*Pit\s*</td>\s*<td[^>]*>\s*(\d+)"),
            Meat1 = ExtractNumber(statushtml, @"<td>\s*Meat\s+1\s*</td>\s*<td[^>]*>\s*(\d+|---)"),
            SetTemperature = ExtractSetTemperature(sethtml),
            BlowerPercentage = ExtractNumber(statushtml, @"<td>\s*Blower\s*</td>\s*<td[^>]*>\s*(\d+|---)")
        };
        LastCheck = DateTime.Now;
        return CurrentStatus;

    }
    
    public string? ExtractSetTemperature(string htmlContent)
    {
        string? result;
        if (string.IsNullOrWhiteSpace(htmlContent)) 
            result = null;
        
        string pattern = @"name=[""']s[""']\s+[^>]*value=[""'](\d+)[""']";
        var match = Regex.Match(htmlContent, pattern, 
            RegexOptions.IgnoreCase);
        
        result = match.Success && int.TryParse(match.Groups[1].Value, out int temp) ? temp.ToString() : null;
        
        return result;
    }
    
    public string? ExtractNumber(string html, string pattern)
    {
        string? result;
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        if (!match.Success) 
            result = null;

        string val = match.Groups[1].Value.Trim();
        result = val == "---" ? null : int.TryParse(val, out int n) ? n.ToString() : null;
        
        return result;
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
}