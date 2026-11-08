namespace Flameboss;

/// <summary>
/// Bound from IConfiguration at startup; keys match property names case-insensitively,
/// so the env vars FLAMEBOSSURL, MQTTBROKER, MQTTUSERNAME, MQTTPASSWORD, DEVICENAME,
/// FRIENDLYNAME and POLLSECONDS (or user-secrets with the same names) set them.
/// </summary>
public record Configuration
{
    public string FlameBossUrl { get; set; } = "";
    public string MqttBroker { get; set; } = "localhost";
    public string? MqttUsername { get; set; }
    public string? MqttPassword { get; set; }
    public string DeviceName { get; set; } = "flameboss";
    public string FriendlyName { get; set; } = "Flame Boss";
    public int PollSeconds { get; set; } = 5;
}
