using System.Text.Json;
using MQTTnet;
namespace Flameboss;

public class MqttService
{
    private readonly IMqttClient _client;
    private readonly Configuration _config;
    private readonly ILogger<MqttService> _logger;
    public FlameBossStatus CurrentTemps { get; private set; } = new();

    public MqttService(Configuration config, ILogger<MqttService> logger)
    {
        _config = config;
        _logger = logger;
        var factory = new MQTTnet.MqttClientFactory();
        _client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(config.MqttBroker ?? "localhost")
            .WithCredentials(config.MqttUsername, config.MqttPassword)
            .WithClientId("FlameBossMonitor")
            .WithCleanSession()
            .Build();

        _client.ConnectAsync(options).Wait();
        PublishHomeAssistantDiscovery();
    }

    public async Task UpdateAndPublish(FlameBossStatus temps)
    {
        CurrentTemps = temps;

        var topics = new[]
        {
            ("pit", temps.Pit),
            ("meat1", temps.Meat1),
            ("blower", temps.BlowerPercentage),
            ("set_temp", temps.SetTemperature),
        };

        foreach (var (name, value) in topics)
        {
            if (value != -1)
            {
                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic($"homeassistant/sensor/flameboss_{name}/state")
                    .WithPayload(value.ToString())
                    .Build();
                await _client.PublishAsync(applicationMessage);
            }
        }
        
    }

    private async void PublishHomeAssistantDiscovery()
    {
        var device = new
        {
            identifiers = new[] { _config.DeviceName },
            name = _config.FriendlyName,
            manufacturer = "Flame Boss",
            model = "WiFi Controller"
        };

        //https://www.home-assistant.io/integrations/sensor/#device-class
        var sensors = new[]
        {
            ("pit", "Pit Temperature", "°F", "temperature"),
            ("meat1", "Meat 1 Probe", "°F", "temperature"),
            ("blower", "Blower", "%", ""),
            ("set_temp", "Set Temp", "°F", "temperature")
        };

        foreach (var (id, name, unit, deviceClass) in sensors)
        {
            string configTopic = $"homeassistant/sensor/flameboss_{id}/config";
            var configPayload = new
            {
                name = $"{_config.FriendlyName} {name}",
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