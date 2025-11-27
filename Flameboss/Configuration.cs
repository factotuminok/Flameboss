namespace Flameboss;

public record Configuration
{
public string FlameBossUrl { get; set; } = "http://192.168.1.178/";
public string MqttBroker { get; set; } = "mqtt.lan";
public string? MqttUsername { get; set; } = "mqtt";
public string? MqttPassword { get; set; } = "MJL4$l3w1s";
public string DeviceName { get; set; } = "flameboss";
public string FriendlyName { get; set; } = "Flame Boss";
}