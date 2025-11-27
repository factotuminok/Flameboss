using Flameboss;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configuration (edit these!)
var config = new Configuration
{
    FlameBossUrl = "http://192.168.1.178/",           // Default Flame Boss AP
    //FlameBossUrl = "http://flameboss.lan/",    // Or use mDNS if on WiFi
    MqttBroker = "mqtt.lan:1883",                   // Your MQTT broker
    MqttUsername = "mqtt",                              // Optional
    MqttPassword = "MJL4$l3w1s",                              // Optional
    DeviceName = "flameboss_bbq",
    FriendlyName = "Flame Boss BBQ"
};

builder.Services.AddSingleton(config);
builder.Services.AddHttpClient();
builder.Services.AddHostedService<FlameBossPollingService>();
builder.Services.AddSingleton<MqttService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
// Public REST API
app.MapGet("/api/temps", (MqttService mqtt) => mqtt.CurrentTemps)
    .WithName("GetCurrentTemps")
    .WithOpenApi();

app.MapGet("/", () => "Flame Boss Monitor is running. Use /api/temps");
app.Run();