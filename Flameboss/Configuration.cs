namespace Flameboss;

/// <summary>
/// Bound from IConfiguration at startup; keys match property names case-insensitively,
/// so the env vars FLAMEBOSSURL, MQTTBROKER, MQTTUSERNAME, MQTTPASSWORD, DEVICENAME, FRIENDLYNAME, POLLSECONDS,
/// UNREACHABLETIMEOUTSECONDS, AUTOSTARTCHECKSECONDS, MONGODBURI and MONGODBDATABASE (or user-secrets with the same names) set them.
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
    /// <summary>Stop the cook after the controller has been unreachable this long (env UNREACHABLETIMEOUTSECONDS).</summary>
    public int UnreachableTimeoutSeconds { get; set; } = 180;
    /// <summary>How often to check, while no cook is running, whether the controller is on and auto-start a cook
    /// (env AUTOSTARTCHECKSECONDS); 0 disables auto-start.</summary>
    public int AutoStartCheckSeconds { get; set; } = 300;
    /// <summary>MongoDB connection string for cook readings; unset disables logging to Mongo.</summary>
    public string? MongoDbUri { get; set; }
    public string MongoDbDatabase { get; set; } = "flameboss";
}
