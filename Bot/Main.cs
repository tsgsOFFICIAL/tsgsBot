using tsgsBot_C_.StateServices;
using Discord.Interactions;
using tsgsBot_C_.Services;
using tsgsBot_C_.Models;
using Discord.WebSocket;
using System.Reflection;
using Discord.Rest;
using Discord;
using tsgsBot_C_.Utils;

namespace tsgsBot_C_.Bot
{

    internal sealed class DiscordBotHostedService(
        DiscordSocketClient client,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        SupportFormStateService supportStateService,
        PollFormStateService pollStateService,
        GiveawayFormStateService giveawayStateService,
        RolePanelFormStateService rolePanelFormStateService,
        IBackgroundTaskQueue backgroundTaskQueue,
        ILogger<DiscordBotHostedService> logger) : BackgroundService
    {
        // Configuration constants
        private const int ACTIVITY_UPDATE_INTERVAL_MINUTES = 5;
        private const int CLEANUP_INTERVAL_MINUTES = 30;
        private const string ENVIRONMENT_VARIABLE_NAME = "ENVIRONMENT";
        private const string PRODUCTION_ENVIRONMENT = "production";

        // Cached service instances to avoid repeated service provider lookups
        private MemberCounterService? _memberCounterService;
        private PollService? _pollService;
        private GiveawayService? _giveawayService;

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
        private PeriodicTimer? _cleanupTimer;
        private CancellationTokenSource? _cleanupCts;

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

            try
            {
                // Cancel all background operations
                _cleanupCts?.Cancel();
                _cts?.Cancel();

                // Dispose timers
                _cleanupTimer?.Dispose();
                _activityTimer?.Dispose();

                // Dispose cancellation tokens
                _cleanupCts?.Dispose();
                _cts?.Dispose();

                await client.StopAsync();
                await client.LogoutAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during shutdown: Failed to cleanly shutdown bot components");
            }
            finally
            {
                await base.StopAsync(cancellationToken);
                logger.LogInformation("Discord bot shutdown completed");
            }
        }

        private Task LogAsync(LogMessage msg)
        {
            logger.LogInformation(msg.ToString());
            return Task.CompletedTask;
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
                logger.LogError(ex, "Activity loop crashed: Unexpected error in periodic activity update");
            }
        }

        private async Task CleanupLoopAsync(CancellationToken ct)
        {
            try
            {
                while (await _cleanupTimer!.WaitForNextTickAsync(ct))
                {
                    try
                    {
                        TimeSpan timeout = TimeSpan.FromMinutes(CLEANUP_INTERVAL_MINUTES);

                        int removed = supportStateService.Cleanup(timeout);
                        if (removed > 0)
                            logger.LogInformation("Cleaned up {Count} expired support form states.", removed);

                        removed = pollStateService.Cleanup(timeout);
                        if (removed > 0)
                            logger.LogInformation("Cleaned up {Count} expired poll form states.", removed);

                        removed = giveawayStateService.Cleanup(timeout);
                        if (removed > 0)
                            logger.LogInformation("Cleaned up {Count} expired giveaway form states.", removed);

                        removed = rolePanelFormStateService.Cleanup(timeout);
                        if (removed > 0)
                            logger.LogInformation("Cleaned up {Count} expired role panel form states.", removed);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Cleanup iteration error: Failed to clean up form states");
                    }
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cleanup loop crashed: Unexpected error in periodic cleanup operation");
            }
        }

        private void StartCleanupTimer()
        {
            _cleanupCts = new CancellationTokenSource();
            _cleanupTimer = new PeriodicTimer(TimeSpan.FromMinutes(CLEANUP_INTERVAL_MINUTES));
            _ = Task.Run(() => CleanupLoopAsync(_cleanupCts.Token));
            logger.LogInformation("Support form state cleanup timer started (every {Minutes} min).", CLEANUP_INTERVAL_MINUTES);
        }

        private async Task<(bool Valid, SocketTextChannel? Channel, IUserMessage? Message)> ValidateAndFetchMessageAsync(string guildIdStr, string channelIdStr, string messageIdStr, Action<string> onInvalid)
        {
            // Parse and validate guild ID
            if (!ulong.TryParse(guildIdStr, out ulong guildId))
            {
                onInvalid("Invalid guild ID format");
                return (false, null, null);
            }

            // Fetch guild
            SocketGuild? guild = client.GetGuild(guildId);
            if (guild == null)
            {
                onInvalid("Guild not found");
                return (false, null, null);
            }

            // Parse and validate channel ID
            if (!ulong.TryParse(channelIdStr, out ulong channelId))
            {
                onInvalid("Invalid channel ID format");
                return (false, null, null);
            }

            // Fetch channel
            SocketTextChannel? channel = guild.GetTextChannel(channelId);
            if (channel == null)
            {
                onInvalid("Channel not found or inaccessible");
                return (false, null, null);
            }

            // Parse and validate message ID
            if (!ulong.TryParse(messageIdStr, out ulong messageId))
            {
                onInvalid("Invalid message ID format");
                return (false, null, null);
            }

            // Fetch message
            if (await channel.GetMessageAsync(messageId) is not IUserMessage message)
            {
                onInvalid("Message not found or deleted");
                return (false, null, null);
            }

            return (true, channel, message);
        }

        private async Task SetRandomActivityAsync()
        {
            (ActivityType type, string? name) = ActivityCombos[Random.Shared.Next(ActivityCombos.Length)];

            try
            {
                await client.SetGameAsync(name, type: type);

                logger.LogInformation("🎮  Activity updated: {Type} {Name}", type, name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to update activity to '{Name}' ({Type}): Discord API may be temporarily unavailable", name, type);
            }
        }

        private async Task OnReadyAsync()
        {
            try
            {
                // Register all Interaction modules (slash commands, modals, etc.)
                await interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);

                string? env = Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE_NAME);

                if (!string.IsNullOrEmpty(env) && env.Equals(PRODUCTION_ENVIRONMENT, StringComparison.OrdinalIgnoreCase))
                {
                    // Unregister all guild-specific commands to avoid duplicates
                    IReadOnlyCollection<RestGuildCommand> guildCommands = await client.Rest.GetGuildApplicationCommands(SharedProperties.Instance.GuildId); // Clean up guild commands

                    foreach (RestGuildCommand command in guildCommands)
                    {
                        if (command != null)
                        {
                            await command.DeleteAsync();
                        }
                    }

                    await interactionService.RegisterCommandsGloballyAsync(); // For production
                }
                else
                {
                    await client.Rest.DeleteAllGlobalCommandsAsync(); // Avoid duplicates during development
                    await interactionService.RegisterCommandsToGuildAsync(SharedProperties.Instance.GuildId); // For local development / testing guild
                }

                _cts = new CancellationTokenSource();

                // Start cycling activities every ~5 minutes
                _activityTimer = new PeriodicTimer(TimeSpan.FromMinutes(ACTIVITY_UPDATE_INTERVAL_MINUTES));
                _ = Task.Run(() => ActivityLoopAsync(_cts.Token));

                // Set initial activity
                await SetRandomActivityAsync();

                _memberCounterService = serviceProvider.GetRequiredService<MemberCounterService>();
                _pollService = serviceProvider.GetRequiredService<PollService>();
                _giveawayService = serviceProvider.GetRequiredService<GiveawayService>();

                // Update member counts
                await _memberCounterService!.UpdateAsync();

                logger.LogInformation("🤖  Logged in as bot with ID {BotId}", client.CurrentUser?.Id);
                logger.LogInformation("✅  Bot is ready and commands registered!");

                SharedProperties.Instance.Init();

                // Init database and resurrect polls, giveaways etc.
                DatabaseService.Instance.Init();

                List<DatabasePollModel> activePolls = await DatabaseService.Instance.GetActivePollsAsync();
                List<DatabaseGiveawayModel> activeGiveaways = await DatabaseService.Instance.GetActiveGiveawaysAsync();
                List<DatabaseReminderModel> activeReminders = await DatabaseService.Instance.GetActiveRemindersAsync();

                // Resurrect active polls
                logger.LogInformation("Resurrecting {Count} active poll(s)...", activePolls.Count);

                foreach (DatabasePollModel poll in activePolls)
                {
                    try
                    {
                        (bool valid, SocketTextChannel? channel, IUserMessage? message) = await ValidateAndFetchMessageAsync(
                            poll.GuildId,
                            poll.ChannelId,
                            poll.MessageId,
                            reason => logger.LogWarning("Skipping poll {PollId}: {Reason}", poll.Id, reason));

                        if (!valid)
                        {
                            await DatabaseService.Instance.UpdatePollEndedAsync(poll.Id);
                            continue;
                        }

                        // Calculate remaining time using UTC for consistency
                        TimeSpan timeLeft = poll.EndTime - DateTime.UtcNow;
                        if (timeLeft <= TimeSpan.Zero)
                        {
                            logger.LogWarning("Poll {PollId} is overdue by {Overdue}. Finalizing immediately.", poll.Id, -timeLeft);

                            // Poll has already expired; finalize immediately
                            if (message != null)
                            {
                                await _pollService!.FinalizePollAsync(message, poll.Question, poll.Answers, poll.Emojis, poll.Id, poll.CreatedByUserId);

                                DatabasePollModel? latestPoll = await DatabaseService.Instance.GetPollAsync(poll.Id);
                                if (latestPoll != null && !latestPoll.HasEnded)
                                {
                                    logger.LogWarning("Poll {PollId} did not finalize cleanly. Marking as ended to stop resurrection loop.", poll.Id);
                                    await DatabaseService.Instance.UpdatePollEndedAsync(poll.Id);
                                }
                            }
                        }
                        else
                        {
                            logger.LogInformation("Resurrected poll {PollId} with {TimeLeft} remaining.", poll.Id, timeLeft);

                            // Queue delayed finalization as a background task for better tracking and reliability
                            BackgroundTask backgroundTask = new BackgroundTask
                            {
                                TaskType = "PollFinalization",
                                Description = $"Poll finalization for poll {poll.Id}",
                                Work = async (ct) =>
                                {
                                    try
                                    {
                                        await DelayHelper.DelayAsync(timeLeft, ct);
                                        if (message != null)
                                            await _pollService!.FinalizePollAsync(message, poll.Question, poll.Answers, poll.Emojis, poll.Id, poll.CreatedByUserId);
                                        logger.LogInformation("Successfully finalized poll {PollId}", poll.Id);
                                    }
                                    catch (TaskCanceledException)
                                    {
                                        logger.LogInformation("Poll {PollId} finalization was cancelled during bot shutdown", poll.Id);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogError(ex, "Error finalizing poll {PollId}", poll.Id);
                                    }
                                }
                            };
                            await backgroundTaskQueue.QueueAsync(backgroundTask);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Catch-all for unexpected errors during resurrection; log and mark as ended
                        logger.LogError(ex, "Error resurrecting poll {PollId}", poll.Id);
                        await DatabaseService.Instance.UpdatePollEndedAsync(poll.Id);
                    }
                }

                // Resurrect active giveaways
                logger.LogInformation("Resurrecting {Count} active giveaway(s)...", activeGiveaways.Count);

                foreach (DatabaseGiveawayModel giveaway in activeGiveaways)
                {
                    try
                    {
                        (bool valid, SocketTextChannel? channel, IUserMessage? message) = await ValidateAndFetchMessageAsync(
                            giveaway.GuildId,
                            giveaway.ChannelId,
                            giveaway.MessageId,
                            reason => logger.LogWarning("Skipping giveaway {GiveawayId}: {Reason}", giveaway.Id, reason));

                        if (!valid)
                        {
                            await DatabaseService.Instance.UpdateGiveawayEndedAsync(giveaway.Id);
                            continue;
                        }

                        // Calculate remaining time using UTC for consistency
                        TimeSpan timeLeft = giveaway.EndTime - DateTime.UtcNow;
                        if (timeLeft <= TimeSpan.Zero)
                        {
                            logger.LogWarning("Giveaway {GiveawayId} is overdue by {Overdue}. Finalizing immediately.", giveaway.Id, -timeLeft);

                            // Giveaway has already expired; finalize immediately
                            if (message != null)
                            {
                                await _giveawayService!.FinalizeGiveawayAsync(message, giveaway.Prize, giveaway.ReactionEmoji, giveaway.Winners.ToString(), giveaway.Id, giveaway.CreatedByUserId);

                                DatabaseGiveawayModel? latestGiveaway = await DatabaseService.Instance.GetGiveawayAsync(giveaway.Id);
                                if (latestGiveaway != null && !latestGiveaway.HasEnded)
                                {
                                    logger.LogWarning("Giveaway {GiveawayId} did not finalize cleanly. Marking as ended to stop resurrection loop.", giveaway.Id);
                                    await DatabaseService.Instance.UpdateGiveawayEndedAsync(giveaway.Id);
                                }
                            }
                        }
                        else
                        {
                            logger.LogInformation("Resurrected giveaway {GiveawayId} with {TimeLeft} remaining.", giveaway.Id, timeLeft);

                            // Queue delayed finalization as a background task for better tracking and reliability
                            BackgroundTask backgroundTask = new BackgroundTask
                            {
                                TaskType = "GiveawayFinalization",
                                Description = $"Giveaway finalization for giveaway {giveaway.Id}",
                                Work = async (ct) =>
                                {
                                    try
                                    {
                                        await DelayHelper.DelayAsync(timeLeft, ct);
                                        if (message != null)
                                            await _giveawayService!.FinalizeGiveawayAsync(message, giveaway.Prize, giveaway.ReactionEmoji, giveaway.Winners.ToString(), giveaway.Id, giveaway.CreatedByUserId);
                                        logger.LogInformation("Successfully finalized giveaway {GiveawayId}", giveaway.Id);
                                    }
                                    catch (TaskCanceledException)
                                    {
                                        logger.LogInformation("Giveaway {GiveawayId} finalization was cancelled during bot shutdown", giveaway.Id);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogError(ex, "Error finalizing giveaway {GiveawayId}", giveaway.Id);
                                    }
                                }
                            };
                            await backgroundTaskQueue.QueueAsync(backgroundTask);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Catch-all for unexpected errors during resurrection; log and mark as ended
                        logger.LogError(ex, "Error resurrecting giveaway {GiveawayId}", giveaway.Id);
                        await DatabaseService.Instance.UpdateGiveawayEndedAsync(giveaway.Id);
                    }
                }

                // Resurrect active reminders
                logger.LogInformation("Resurrecting {Count} active reminder(s)...", activeReminders.Count);

                foreach (DatabaseReminderModel reminder in activeReminders)
                {
                    try
                    {
                        // Calculate remaining time using UTC for consistency
                        TimeSpan timeLeft = reminder.ReminderTime - DateTime.UtcNow;
                        logger.LogInformation("Resurrected reminder {ReminderId} with {TimeLeft} remaining.", reminder.Id, timeLeft);

                        if (timeLeft <= TimeSpan.Zero)
                        {
                            // Reminder has already expired; send immediately
                            SocketUser user = client.GetUser(reminder.UserId);
                            if (user != null)
                            {
                                try
                                {
                                    await user.SendMessageAsync($"🔔 **Reminder:** {reminder.Task}");
                                    await DatabaseService.Instance.MarkReminderSentAsync(reminder.Id);
                                    logger.LogInformation("Immediately sent expired reminder {ReminderId}", reminder.Id);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError(ex, "Failed to send expired reminder {ReminderId} to user {UserId}", reminder.Id, reminder.UserId);
                                }
                            }
                        }
                        else
                        {
                            // Queue delayed reminder as a background task
                            BackgroundTask backgroundTask = new BackgroundTask
                            {
                                TaskType = "Reminder",
                                Description = $"Reminder for user {reminder.UserId}: {reminder.Task}",
                                Work = async (ct) =>
                                {
                                    try
                                    {
                                        await DelayHelper.DelayAsync(timeLeft, ct);

                                        DatabaseReminderModel? latestReminder = await DatabaseService.Instance.GetReminderAsync(reminder.Id);
                                        if (latestReminder is null || latestReminder.HasSent)
                                        {
                                            logger.LogInformation("Reminder {ReminderId} is no longer active; skipping send", reminder.Id);
                                            return;
                                        }

                                        SocketUser user = client.GetUser(latestReminder.UserId);
                                        if (user != null)
                                        {
                                            await user.SendMessageAsync($"🔔 **Reminder:** {latestReminder.Task}");
                                            logger.LogInformation("Reminder sent for ReminderId {ReminderId}", reminder.Id);
                                        }
                                        else
                                        {
                                            logger.LogWarning("Could not find user {UserId} to send reminder {ReminderId}", latestReminder.UserId, reminder.Id);
                                        }

                                        // Mark reminder as sent in database
                                        await DatabaseService.Instance.MarkReminderSentAsync(reminder.Id);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        logger.LogInformation("Reminder {ReminderId} was cancelled during bot shutdown", reminder.Id);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogError(ex, "Error sending reminder {ReminderId}", reminder.Id);
                                    }
                                }
                            };
                            await backgroundTaskQueue.QueueAsync(backgroundTask);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error resurrecting reminder {ReminderId}", reminder.Id);
                    }
                }

                StartCleanupTimer();

                logger.LogInformation("✅  Bot initialization complete: {PollCount} polls, {GiveawayCount} giveaways, and {ReminderCount} reminders resurrected",
                    activePolls.Count, activeGiveaways.Count, activeReminders.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed during ready handling: Error during bot initialization, command registration, or resource resurrection");
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
                await _memberCounterService!.UpdateAsync();

                logger.LogInformation("New member joined: {Username} ({UserId}) in guild {GuildName}", user.Username, user.Id, guild.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling guild member added for user {UserId} in guild {GuildId}: Failed to send welcome message or update member counter",
                    user?.Id, user?.Guild?.Id);
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
                await _memberCounterService!.UpdateAsync();

                logger.LogInformation("Member left: {Username} ({UserId}) from guild {GuildName}", user.Username, user.Id, guild.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling guild member removed for user {UserId} from guild {GuildId}: Failed to send farewell message or update member counter",
                    user?.Id, guild?.Id);
            }
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
                logger.LogError(ex, "Interaction execution failed for interaction {InteractionId} of type {InteractionType} from user {UserId}",
                    interaction.Id, interaction.Type, interaction.User.Id);
            }
        }
    }
}