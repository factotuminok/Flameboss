using Flameboss;
using Flameboss.Components;
using MudBlazor.Services;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Settings come from env vars (the k8s Deployment and its Secret) or, in Development, dotnet user-secrets.
var config = builder.Configuration.Get<Configuration>() ?? new Configuration();
if (string.IsNullOrWhiteSpace(config.FlameBossUrl))
    throw new InvalidOperationException("FLAMEBOSSURL is not set");
if (!config.FlameBossUrl.EndsWith('/'))
    config.FlameBossUrl += "/";
if (config.PollSeconds < 1)
    throw new InvalidOperationException("POLLSECONDS must be at least 1");
if (config.UnreachableTimeoutSeconds < 1)
    throw new InvalidOperationException("UNREACHABLETIMEOUTSECONDS must be at least 1");
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Add services
builder.Services.AddSingleton(config);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MqttService>();
builder.Services.AddSingleton<CookReadingStore>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CookReadingStore>());
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
await using var scope = app.Services.CreateAsyncScope();
app.UseHttpsRedirection();

// Public REST API
app.MapGet("/api", () => "Flame Boss Monitor is running. Use /api/status");
app.MapGet("/heartbeat", () => "Alive");
app.MapControllers();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var logger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger<Program>();
logger.LogInformation("Flameboss application starting.");

app.Run();