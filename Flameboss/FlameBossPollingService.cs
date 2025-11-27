using System.Text.RegularExpressions;

namespace Flameboss;

public class FlameBossPollingService: BackgroundService
{
    private readonly HttpClient _http;
    private readonly MqttService _mqtt;
    private readonly string _url;
    private readonly ILogger<FlameBossPollingService> _logger;

    public FlameBossPollingService(HttpClient http, MqttService mqtt, Configuration config, ILogger<FlameBossPollingService> logger)
    {
        _http = http;
        _mqtt = mqtt;
        _url = config.FlameBossUrl.TrimEnd('/');
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                string html = await _http.GetStringAsync(_url, stoppingToken);

                string pit = ExtractNumber(html, @"<td>\s*Pit\s*</td>\s*<td[^>]*>\s*(\d+)");
                string meat1 = ExtractNumber(html, @"<td>\s*Meat\s+1\s*</td>\s*<td[^>]*>\s*(\d+|---)");
                string blower = ExtractNumber(html, @"<td>\s*Blower\s*</td>\s*<td[^>]*>\s*(\d+|---)");
                html = await _http.GetStringAsync(_url + "/set", stoppingToken);
                string setTemp = ExtractSetTemperature(html);

                var temps = new Temperatures(
                    Pit: pit,
                    Meat1: meat1,
                    SetTemp: setTemp,
                    Blower: blower,
                    LastUpdate: DateTime.Now
                );

                await _mqtt.UpdateAndPublish(temps);

                _logger.LogInformation("Updated → Pit: {Pit}°F, Meat1: {Meat1}°F, Blower {blower}%, SetTemp {setTemp}°F ", 
                    pit, meat1, blower, setTemp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch or parse Flame Boss data");
                await Task.Delay(60000, stoppingToken); // Every 60 seconds
            }

            await Task.Delay(5000, stoppingToken); // Every 5 seconds
        }
    }

    private static string ExtractNumber(string html, string pattern)
    {
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        if (!match.Success) return "-1";

        string val = match.Groups[1].Value.Trim();
        return val == "---" ? "-1" : int.TryParse(val, out int n) ? n.ToString() : "-1";
    }
    
    /// <summary>
    /// Extracts the current set temperature from Flame Boss HTML page content.
    /// Looks for value attribute in input with name='s'
    /// </summary>
    /// <param name="htmlContent">The full HTML content as string</param>
    /// <returns>The set temperature as int, or null if not found</returns>
    public static string ExtractSetTemperature(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            return "-1";

        // Regex patterns to find value in: name='s' or name="s" and value='177' or value="177"
        string pattern = @"name=[""']s[""']\s+[^>]*value=[""'](\d+)[""']";
        
        var match = Regex.Match(htmlContent, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        if (match.Success && match.Groups.Count > 1)
        {
            if (int.TryParse(match.Groups[1].Value, out int temperature))
            {
                return temperature.ToString();
            }
        }

        // Fallback: more permissive search for value= followed by number near "Set Temp"
        string fallbackPattern = @"Set\s+Temp[^0-9]*(\d+)";
        match = Regex.Match(htmlContent, fallbackPattern, RegexOptions.IgnoreCase);
        
        if (match.Success && match.Groups.Count > 1)
        {
            if (int.TryParse(match.Groups[1].Value, out int temperature))
            {
                return temperature.ToString();
            }
        }

        return "-1";
    }
}