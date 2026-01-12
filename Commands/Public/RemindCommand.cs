using Discord.Interactions;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class RemindCommand : LoggedCommandModule
    {
        [SlashCommand("remind", "Set a reminder for a task at a specific time")]
        [CommandContextType(InteractionContextType.Guild | InteractionContextType.BotDm)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task RemindAsync(
            [Summary("task", "The task or item to remind you about")] string task,
            [Summary("time", "Time in 24-hour format (e.g. 14:30)")] string time,
            [Summary("date", "Date in YYYY-MM-DD format (optional – defaults to today)")] string? date = null)
        {
            await DeferAsync(ephemeral: true);
            await LogCommandAsync(("task", task), ("time", time), ("date", date));

            try
            {
                // Default to today if no date
                date ??= DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

                // Parse date + time
                if (!DateTimeOffset.TryParse($"{date} {time}", out DateTimeOffset reminderTime))
                {
                    await FollowupAsync("Invalid date/time format. Use YYYY-MM-DD and HH:mm (24-hour).", ephemeral: true);
                    return;
                }

                DateTimeOffset now = DateTimeOffset.UtcNow;

                if (reminderTime <= now)
                {
                    await FollowupAsync("That time has already passed. Please choose a future time.", ephemeral: true);
                    return;
                }

                TimeSpan delay = reminderTime - now;

                // Human-readable delay
                List<string> parts = new List<string>();
                if (delay.Days > 0) parts.Add($"{delay.Days} day{(delay.Days > 1 ? "s" : "")}");
                if (delay.Hours > 0) parts.Add($"{delay.Hours} hour{(delay.Hours > 1 ? "s" : "")}");
                if (delay.Minutes > 0) parts.Add($"{delay.Minutes} minute{(delay.Minutes > 1 ? "s" : "")}");
                if (delay.Seconds > 0 || parts.Count == 0)
                    parts.Add($"{delay.Seconds} second{(delay.Seconds > 1 ? "s" : "")}");

                string delayText = string.Join(", ", parts);

                await FollowupAsync(
                    $"Reminder set for **{task}** at {reminderTime:yyyy-MM-dd HH:mm} UTC.\n" +
                    $"I will remind you in {delayText}.",
                    ephemeral: true);

                // Fire reminder after delay (non-persistent!)
                _ = Task.Delay(delay).ContinueWith(async _ =>
                {
                    try
                    {
                        await Context.User.SendMessageAsync($"🔔 Reminder: **{task}**");
                    }
                    catch (Exception ex)
                    {
                        // User might have DMs closed → log or ignore
                        Console.WriteLine($"Failed to send reminder DM: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error setting reminder: {ex.Message}", ephemeral: true);
            }
        }
    }
}