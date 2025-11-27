using System.Text.RegularExpressions;

namespace Flameboss;

public class Parser
{
    public bool Extract()
    {
        var client = new System.Net.WebClient();
        string html = client.DownloadString("http://192.168.1.178/"); // typical Flame Boss AP IP
        int pitTemp = ExtractPitTemperature(html);
        int meatTemp = ExtractMeat1Temperature(html);
        Console.WriteLine(pitTemp == -1 ? "Not connected" : $"Pit: {pitTemp}°F");
        Console.WriteLine(pitTemp == -1 ? "Not connected" : $"Meat: {meatTemp}°F");
        return pitTemp == -1 || meatTemp == -1;
    }
        
    /// <summary>
    /// Extracts the Pit temperature from Flame Boss controller HTML.
    /// Looks for pattern: <td>Pit</td><td align='right'>177<br/></td>
    /// </summary>
    /// <param name="html">The full HTML source as string</param>
    /// <returns>Pit temperature in degrees F, or -1 if not found</returns>
    public static int ExtractPitTemperature(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return -1;

        // Regex pattern to match the Pit temperature line
        // Captures the number between > and <br/> after "Pit"
        string pattern = @"<td>\s*Pit\s*</td>\s*<td[^>]*>\s*(\d+)\s*<br\s*/?>";

        Match match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            if (int.TryParse(match.Groups[1].Value, out int temperature))
            {
                return temperature;
            }
        }

        return -1; // Not found or invalid
    }
    
    /// <summary>
    /// Extracts Meat 1 temperature (e.g., 178 in your sample)
    /// </summary>
    public static int ExtractMeat1Temperature(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return -1;

        // Pattern matches: <td>Meat 1</td><td align='right'>178<br/></td>
        string pattern = @"<td>\s*Meat\s+1\s*</td>\s*<td[^>]*>\s*(?<temp>\d+|---)\s*<br";

        Match match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            string value = match.Groups["temp"].Value.Trim();
                
            if (value == "---" || string.IsNullOrEmpty(value))
                return -1; // Not connected / no probe

            if (int.TryParse(value, out int temp))
                return temp;
        }

        return -1; // Not found
    }
}
