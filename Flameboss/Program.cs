using Flameboss;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configuration (edit these!)
var config = new Configuration
{
    FlameBossUrl = Environment.GetEnvironmentVariable("FLAMEBOSSURL"),      // Default Flame Boss AP
    MqttBroker = Environment.GetEnvironmentVariable("MQTTBROKER"),               // Your MQTT broker
    MqttUsername = Environment.GetEnvironmentVariable("MQTTUSERNAME"),                      // MQTT Username
    MqttPassword = Environment.GetEnvironmentVariable("MQTTPASSWORD"),                // MQTT Password
    DeviceName = Environment.GetEnvironmentVariable("DEVICENAME"),
    FriendlyName = Environment.GetEnvironmentVariable("FRIENDLYNAME"),
    PollSeconds = Environment.GetEnvironmentVariable("POLLSECONDS"),
};

// Add services
builder.Services.AddSingleton(config);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MqttService>();
builder.Services.AddSingleton<FlameBossService>();
builder.Services.AddHostedService<FlameBossPollingService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Public REST API
app.MapGet("/", () => "Flame Boss Monitor is running. Use /api/status");
app.MapGet("/heartbeat", () => "Alive");
app.MapControllers();
app.Run();