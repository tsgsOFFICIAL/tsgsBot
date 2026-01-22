using tsgsBot_C_.StateServices;
using Discord.Interactions;
using tsgsBot_C_.Services;
using Discord.WebSocket;
using tsgsBot_C_.Bot;
using tsgsBot_C_;
using Discord;

Console.OutputEncoding = System.Text.Encoding.UTF8;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ────────────────────────────────────────
// 1. Discord & bot services
// ────────────────────────────────────────
builder.Services.AddSingleton<DiscordSocketClient>(sp =>
{
    DiscordSocketConfig config = new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.All
    };
    return new DiscordSocketClient(config);
});

builder.Services.AddSingleton<InteractionService>(sp =>
{
    DiscordSocketClient client = sp.GetRequiredService<DiscordSocketClient>();
    InteractionServiceConfig config = new InteractionServiceConfig
    {
        // Optional settings – you can leave empty or customize
        DefaultRunMode = RunMode.Async,
        LogLevel = LogSeverity.Info
    };
    return new InteractionService(client.Rest, config);
});

builder.Services.AddSingleton<SupportFormStateService>();
builder.Services.AddSingleton<PollFormStateService>();
builder.Services.AddSingleton<PollService>();
builder.Services.AddSingleton<GiveawayFormStateService>();
builder.Services.AddSingleton<GiveawayService>();
builder.Services.AddSingleton<RolePanelFormStateService>();
builder.Services.AddSingleton<MemberCounterService>();

// Background task queue for managing delayed operations
builder.Services.AddSingleton<IBackgroundTaskQueue>(new BackgroundTaskQueue());
builder.Services.AddHostedService<BackgroundTaskProcessor>();

// A hosted service that manages lifetime of the Discord connection + command registration
builder.Services.AddHostedService<DiscordBotHostedService>();

// ────────────────────────────────────────
// 2. A health checks
builder.Services.AddHealthChecks();

// ────────────────────────────────────────
// Build the app
// ────────────────────────────────────────
string port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

WebApplication app = builder.Build();

// ────────────────────────────────────────
// Health check endpoint that supports both GET and HEAD (important for UptimeRobot)
DateTime startTime = DateTime.UtcNow;
app.MapMethods("/", ["GET", "HEAD"], (HttpContext ctx) =>
{
    if (ctx.Request.Method == "HEAD")
    {
        return Results.Ok();
    }

    var healthStatus = new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        uptime = DateTimeOffset.UtcNow - SharedProperties.Instance.UpTime,
        version = "1.0",
        environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "development"
    };

    return Results.Json(healthStatus);
});

// ────────────────────────────────────────
// Run everything
// ────────────────────────────────────────
await app.RunAsync();