using tsgsBot_C_.StateServices;
using Discord.Interactions;
using tsgsBot_C_.Services;
using Discord.WebSocket;
using System.Reflection;
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
builder.Services.AddSingleton<MemberCounterService>();

// Optional: add hosted service that manages lifetime of the Discord connection + command registration
builder.Services.AddHostedService<DiscordBotHostedService>();

// ────────────────────────────────────────
// 2. Optional: add health checks (very useful when hosting in containers/k8s)
builder.Services.AddHealthChecks();

// ────────────────────────────────────────
// Build the app
// ────────────────────────────────────────
string port = Environment.GetEnvironmentVariable("PORT") ?? "8080"; // Fallback for local dev
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

WebApplication app = builder.Build();

// ────────────────────────────────────────
// Health check endpoint that supports both GET and HEAD (important for UptimeRobot)
app.MapMethods("/", ["GET", "HEAD"], () => Results.Text("tsgsBot is online! ❤️"));

// ────────────────────────────────────────
// Run everything
// ────────────────────────────────────────
await app.RunAsync();

// ─────────────────────────────────────────────────────────────────────────────
//                          Hosted Service – the real bot logic
// ─────────────────────────────────────────────────────────────────────────────
internal sealed class DiscordBotHostedService(DiscordSocketClient client, InteractionService interactionService, IServiceProvider serviceProvider, SupportFormStateService supportStateService, PollFormStateService pollStateService, ILogger<DiscordBotHostedService> logger) : BackgroundService
{
    private static readonly (ActivityType Type, string Name)[] ActivityCombos =
    [
        // Counter-Strike
        (ActivityType.Playing,   "Playing CS with 0 brain cells"),
        (ActivityType.Playing,   "Playing CS with my last shred of hope"),
        (ActivityType.Watching,  "Watching B get rushed, no survivors"),
        (ActivityType.Streaming, "Streaming CS crash my will to live"),
        (ActivityType.Playing,   "Playing Mirage like it's still 2016"),
        (ActivityType.Watching,  "Watching teammates dry peek AWPs"),
        (ActivityType.Listening, "Listening to \"he's one HP\" lies"),
        (ActivityType.Competing, "Competing in blame-the-Igl Olympics"),

        // Rust
        (ActivityType.Playing,   "Playing Rust with 1 rock and 0 hope"),
        (ActivityType.Watching,  "Watching my base decay in real time"),
        (ActivityType.Listening, "Listening to footsteps that aren't mine"),
        (ActivityType.Competing, "Competing in naked Olympics at Outpost"),
        (ActivityType.Streaming, "Streaming my 8th raid failure today"),
        (ActivityType.Playing,   "Playing hide and seek with roof campers"),
        (ActivityType.Watching,  "Watching my scrap vanish at Bandit Camp"),
        (ActivityType.Listening, "Listening to AK shots lull me to sleep"),
        (ActivityType.Streaming, "Streaming solo wipe simulator 2025"),
        (ActivityType.Playing,   "Playing Rust like trust still exists"),
        (ActivityType.Watching,  "Watching a doorcamp ruin my evening"),

        // Generic gamer despair
        (ActivityType.Competing, "Competing in a losing streak tournament"),
        (ActivityType.Watching,  "Watching my K/D tank harder than my life"),
        (ActivityType.Listening, "Listening to teammates scream while I mute"),
        (ActivityType.Playing,   "Playing hide and seek with my sanity"),
        (ActivityType.Listening, "Listening to the void whisper back"),
        (ActivityType.Watching,  "Watching my teammates throw harder than me"),
        (ActivityType.Listening, "Listening to the sound of my hopes shatter"),
        (ActivityType.Streaming, "Streaming my descent into madness"),
        (ActivityType.Playing,   "Playing ranked like MMR is imaginary"),
        (ActivityType.Watching,  "Watching patch notes nerf my main"),
        (ActivityType.Listening, "Listening to Discord arguments at 3AM"),

        // Gambling / bad decisions
        (ActivityType.Playing,   "Playing slots with rent money"),
        (ActivityType.Watching,  "Watching my balance hit zero in slow motion"),
        (ActivityType.Listening, "Listening to the sound of another failed 50/50"),
        (ActivityType.Competing, "Competing in a tournament of poor decisions"),
        (ActivityType.Streaming, "Streaming the downfall of a once-stable man"),
        (ActivityType.Playing,   "Playing coinflip with my remaining dignity"),
        (ActivityType.Watching,  "Watching a 0.1% jackpot I won't win"),
        (ActivityType.Listening, "Listening to the roulette wheel mock me"),
        (ActivityType.Streaming, "Streaming rock bottom in QHD"),
        (ActivityType.Playing,   "Playing odds that are definitely rigged"),
    ];

    private PeriodicTimer? _activityTimer;
    private CancellationTokenSource? _cts;
    private System.Timers.Timer? _cleanupTimer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to events
        client.Log += LogAsync;
        client.Ready += OnReadyAsync;
        client.UserJoined += OnGuildMemberAdded;
        client.UserLeft += OnGuildMemberRemoved;
        client.InteractionCreated += HandleInteractionAsync;

        // Start Discord connection
        try
        {
            await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? throw new InvalidOperationException("Discord token missing"));
            await client.StartAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to start Discord client");
            throw;
        }

        // Keep running until the host is shutting down
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Discord bot is shutting down...");

        _cleanupTimer?.Stop();
        _cleanupTimer?.Dispose();

        await client.StopAsync();
        await client.LogoutAsync();

        await base.StopAsync(cancellationToken);
    }

    private Task LogAsync(LogMessage msg)
    {
        logger.LogInformation(msg.ToString());
        return Task.CompletedTask;
    }

    private async Task OnReadyAsync()
    {
        try
        {
            // Register all Interaction modules (slash commands, modals, etc.)
            await interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);

            string? env = Environment.GetEnvironmentVariable("ENVIRONMENT");

            if (!string.IsNullOrEmpty(env) && env.Equals("production", StringComparison.OrdinalIgnoreCase))
                await interactionService.RegisterCommandsGloballyAsync(); // For production
            else
                await interactionService.RegisterCommandsToGuildAsync(227048721710317569); // For local development / testing guild

            _cts = new CancellationTokenSource();

            // Start cycling activities every ~5 minutes
            _activityTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            _ = Task.Run(() => ActivityLoopAsync(_cts.Token));

            // Set initial activity
            await SetRandomActivityAsync();

            // Update member counts
            MemberCounterService memberCounter = serviceProvider.GetRequiredService<MemberCounterService>();
            await memberCounter.UpdateAsync();

            logger.LogInformation("🤖 Logged in as bot with ID {BotId}", client.CurrentUser?.Id);
            logger.LogInformation("✅ Bot is ready and commands registered!");

            SharedProperties.Instance.Initialize();

            // TODO: Add poll finalization (DB) here if needed

            StartCleanupTimer();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed during ready handling");
        }
    }

    private async Task OnGuildMemberAdded(SocketGuildUser user)
    {
        try
        {
            SocketGuild guild = user.Guild;

            // Welcome message in the system channel (if set)
            if (guild.SystemChannel.Id != 0)
            {
                SocketTextChannel channel = guild.GetTextChannel(guild.SystemChannel.Id);
                if (channel != null)
                {
                    await channel.SendMessageAsync($"Welcome to the server, {user.Mention}! 🎉");
                }
            }

            // Update member counter
            MemberCounterService memberCounter = serviceProvider.GetRequiredService<MemberCounterService>();
            await memberCounter.UpdateAsync();

            logger.LogInformation("New member joined: {Username} ({UserId}) in guild {GuildName}", user.Username, user.Id, guild.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling guild member added for user {UserId}", user?.Id);
        }
    }

    private async Task OnGuildMemberRemoved(SocketGuild guild, SocketUser user)
    {
        try
        {
            // Goodbye message in the system channel (if set)
            if (guild.SystemChannel.Id != 0)
            {
                SocketTextChannel channel = guild.GetTextChannel(guild.SystemChannel.Id);
                if (channel != null)
                {
                    await channel.SendMessageAsync($"{user.Mention} has left the server. 😢");
                }
            }

            // Update member counter
            MemberCounterService memberCounter = serviceProvider.GetRequiredService<MemberCounterService>();
            await memberCounter.UpdateAsync();

            logger.LogInformation("Member left: {Username} ({UserId}) from guild {GuildName}", user.Username, user.Id, guild.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling guild member removed for user {UserId}", user?.Id);
        }
    }

    private async Task ActivityLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _activityTimer!.WaitForNextTickAsync(ct))
            {
                await SetRandomActivityAsync();
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            logger.LogError(ex, "Activity loop crashed");
        }
    }

    private async Task SetRandomActivityAsync()
    {
        (ActivityType type, string? name) = ActivityCombos[Random.Shared.Next(ActivityCombos.Length)];

        try
        {
            await client.SetGameAsync(name, type: type);

            logger.LogInformation("🎮 Activity updated: {Type} {Name}", type, name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update activity to {Name}", name);
        }
    }

    private void StartCleanupTimer()
    {
        _cleanupTimer = new System.Timers.Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
        _cleanupTimer.AutoReset = true;
        _cleanupTimer.Elapsed += async (s, e) =>
        {
            try
            {
                int removed = supportStateService.Cleanup(TimeSpan.FromMinutes(30));
                if (removed > 0)
                    logger.LogInformation("Cleaned up {Count} expired support form states.", removed);

                removed = pollStateService.Cleanup(TimeSpan.FromHours(30));
                if (removed > 0)
                    logger.LogInformation("Cleaned up {Count} expired poll form states.", removed);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cleanup timer error");
            }
        };

        _cleanupTimer.Start();
        logger.LogInformation("Support form state cleanup timer started (every 30 min).");
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            SocketInteractionContext context = new SocketInteractionContext(client, interaction);
            await interactionService.ExecuteCommandAsync(context, serviceProvider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Interaction execution failed");
        }
    }
}