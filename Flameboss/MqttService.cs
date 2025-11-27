using System.Text.Json;
using MQTTnet;
namespace Flameboss;

public class MqttService
{
    private readonly IMqttClient _client;
    private readonly IConfiguration _config;
    public Temperatures CurrentTemps { get; private set; } = new();

    public MqttService(IConfiguration config)
    {
        _config = config;
        var factory = new MQTTnet.MqttClientFactory();
        _client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(Environment.GetEnvironmentVariable("MQTTBROKER") ?? "localhost")
            .WithCredentials(Environment.GetEnvironmentVariable("MQTTUSERNAME"), Environment.GetEnvironmentVariable("MQTTPASSWORD"))
            .WithClientId("FlameBossMonitor")
            .WithCleanSession()
            .Build();

        _client.ConnectAsync(options).Wait();
        PublishHomeAssistantDiscovery();
    }

    public async Task UpdateAndPublish(Temperatures temps)
    {
        CurrentTemps = temps;

        var topics = new[]
        {
            ("pit", temps.Pit),
            ("meat1", temps.Meat1),
            ("blower", temps.Blower),
            ("set_temp", temps.SetTemp),
        };

        foreach (var (name, value) in topics)
        {
            if (value != "-1" && value != "---")
            {
                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic($"homeassistant/sensor/flameboss_{name}/state")
                    .WithPayload(value)
                    .Build();
                await _client.PublishAsync(applicationMessage);
            }
        }
        
    }

    private async void PublishHomeAssistantDiscovery()
    {
        var device = new
        {
            identifiers = new[] { Environment.GetEnvironmentVariable("DEVICENAME") },
            name = Environment.GetEnvironmentVariable("FRIENDLYNAME"),
            manufacturer = "Flame Boss",
            model = "WiFi Controller"
        };

        var sensors = new[]
        {
            ("pit", "Pit Temperature", "°F", "temperature"),
            ("meat1", "Meat 1 Probe", "°F", "temperature"),
            ("blower", "Blower", "%", "percentage"),
            ("set_temp", "Set Temp", "°F", "temperature")
        };

        foreach (var (id, name, unit, deviceClass) in sensors)
        {
            string configTopic = $"homeassistant/sensor/flameboss_{id}/config";
            var configPayload = new
            {
                name = $"{Environment.GetEnvironmentVariable("FRIENDLYNAME")} {name}",
                unique_id = $"flameboss_{id}",
                state_topic = $"homeassistant/sensor/flameboss_{id}/state",
                unit_of_measurement = unit,
                device_class = deviceClass,
                device
            };
            var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(configTopic)
                .WithPayload(JsonSerializer.Serialize(configPayload))
                .Build();
            
            await _client.PublishAsync(applicationMessage);
        }
    }
}