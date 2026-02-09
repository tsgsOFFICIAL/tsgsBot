using Discord.Interactions;
using tsgsBot_C_.Services;
using tsgsBot_C_.Models;
using Discord.WebSocket;
using tsgsBot_C_.Utils;
using Discord;

namespace tsgsBot_C_.Bot.Commands.Public
{
    public sealed class RemindCommand(
        IBackgroundTaskQueue backgroundTaskQueue,
        ILogger<RemindCommand> logger) : LoggedCommandModule
    {
        [SlashCommand("remind", "Set a reminder for a task at a specific time")]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task RemindAsync(
            [Summary("task", "The task or item to remind you about")] string task,
            [Summary("duration", "When to remind you (e.g. 7h 30m, 2d, 1w)")] string duration)
        {
            await DeferAsync(ephemeral: true);
            await LogCommandAsync(("task", task), ("duration", duration));

            try
            {
                TimeSpan? delay = HelperMethods.ParseDuration(duration);

                if (delay is null || delay.Value <= TimeSpan.Zero)
                {
                    await FollowupAsync(
                        "❌ I couldn't understand that time.\n" +
                        "Try something like `10m`, `1h 30m`, or `2d`.",
                        ephemeral: true);
                    return;
                }

                DateTimeOffset reminderTime = DateTimeOffset.UtcNow.Add(delay.Value);

                // Human-readable delay
                List<string> parts = new();

                if (delay.Value.Days > 0)
                    parts.Add($"{delay.Value.Days} day{(delay.Value.Days > 1 ? "s" : "")}");
                if (delay.Value.Hours > 0)
                    parts.Add($"{delay.Value.Hours} hour{(delay.Value.Hours > 1 ? "s" : "")}");
                if (delay.Value.Minutes > 0)
                    parts.Add($"{delay.Value.Minutes} minute{(delay.Value.Minutes > 1 ? "s" : "")}");
                if (delay.Value.Seconds > 0 || parts.Count == 0)
                    parts.Add($"{delay.Value.Seconds} second{(delay.Value.Seconds > 1 ? "s" : "")}");

                string delayText = string.Join(", ", parts);

                await FollowupAsync(
                    $"Reminder set for **{task}** at <t:{reminderTime.ToUnixTimeSeconds()}:F>.\n" +
                    $"I will remind you in {delayText}.",
                    ephemeral: true);

                // Store reminder in database for persistence
                int reminderId = await DatabaseService.Instance.CreateReminderAsync(
                    Context.User.Id,
                    task,
                    reminderTime.UtcDateTime);

                logger.LogInformation("Reminder created: {ReminderId} for user {UserId} at {ReminderTime}", reminderId, Context.User.Id, reminderTime);

                // Queue reminder as a background task
                BackgroundTask backgroundTask = new BackgroundTask
                {
                    TaskType = "Reminder",
                    Description = $"Reminder: {task}",
                    Work = async (ct) =>
                    {
                        try
                        {
                            TimeSpan timeLeft = reminderTime.UtcDateTime - DateTime.UtcNow;
                            logger.LogInformation("Reminder task started for ReminderId {ReminderId}, waiting {TimeLeft} until reminder time", reminderId, timeLeft);

                            if (timeLeft > TimeSpan.Zero)
                                await Task.Delay(timeLeft, ct);

                            DatabaseReminderModel? reminder = await DatabaseService.Instance.GetReminderAsync(reminderId);
                            if (reminder is null || reminder.HasSent)
                            {
                                logger.LogInformation("Reminder {ReminderId} is no longer active; skipping send", reminderId);
                                return;
                            }

                            // Send reminder via DM
                            SocketUser user = Context.Client.GetUser(Context.User.Id);
                            if (user != null)
                            {
                                await user.SendMessageAsync($"🔔 **Reminder:** {reminder.Task}");
                                logger.LogInformation("Reminder sent for ReminderId {ReminderId}", reminderId);
                            }
                            else
                            {
                                logger.LogWarning("Could not find user {UserId} to send reminder {ReminderId}", Context.User.Id, reminderId);
                            }

                            // Mark reminder as sent in database
                            await DatabaseService.Instance.MarkReminderSentAsync(reminderId);
                        }
                        catch (OperationCanceledException)
                        {
                            logger.LogInformation("Reminder {ReminderId} was cancelled during bot shutdown", reminderId);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error sending reminder {ReminderId}", reminderId);
                        }
                    }
                };

                await backgroundTaskQueue.QueueAsync(backgroundTask);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error setting reminder: {ex.Message}", ephemeral: true);
            }
        }
    }
}