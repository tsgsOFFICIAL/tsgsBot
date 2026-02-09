using Discord.Interactions;
using tsgsBot_C_.Services;
using tsgsBot_C_.Models;
using Discord;
using Discord.WebSocket;

namespace tsgsBot_C_.Bot.Commands.Public
{
    public sealed class MyRemindersCommand : LoggedCommandModule
    {
        private const int MaxDeleteOptions = 25;
        private const int MaxLabelLength = 80;

        [SlashCommand("myreminders", "View all your active reminders")]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task MyRemindersAsync()
        {
            await DeferAsync(ephemeral: true);
            await LogCommandAsync();

            try
            {
                List<DatabaseReminderModel> reminders = await DatabaseService.Instance.GetUserRemindersAsync(Context.User.Id);

                if (reminders.Count == 0)
                {
                    await FollowupAsync(
                        "📭 You don't have any reminders.",
                        ephemeral: true);
                    return;
                }

                // Filter to only active (not sent) reminders
                List<DatabaseReminderModel> activeReminders = reminders.Where(r => !r.HasSent).ToList();

                if (activeReminders.Count == 0)
                {
                    await FollowupAsync(
                        "📭 You don't have any active reminders.",
                        ephemeral: true);
                    return;
                }

                (Embed embed, MessageComponent? components) = BuildRemindersMessage(Context.User, activeReminders);

                await FollowupAsync(embed: embed, components: components, ephemeral: true);
            }
            catch (Exception)
            {
                await FollowupAsync(
                    "❌ An error occurred while retrieving your reminders.",
                    ephemeral: true);
                throw;
            }
        }

        [SlashCommand("reminder-delete", "Delete one of your active reminders by ID")]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task ReminderDeleteAsync(
            [Summary("id", "The reminder ID to delete")] int id)
        {
            await DeferAsync(ephemeral: true);
            await LogCommandAsync(("id", id));

            if (id <= 0)
            {
                await FollowupAsync("❌ Reminder ID must be a positive number.", ephemeral: true);
                return;
            }

            bool deleted = await DatabaseService.Instance.DeleteUserReminderAsync(Context.User.Id, id);
            if (!deleted)
            {
                await FollowupAsync("❌ That reminder doesn't exist or was already sent.", ephemeral: true);
                return;
            }

            await FollowupAsync($"✅ Deleted reminder {id}.", ephemeral: true);
        }

        [ComponentInteraction("reminder-delete")]
        public async Task HandleReminderDeleteAsync(string[] values)
        {
            if (values.Length == 0)
                return;

            if (!int.TryParse(values[0], out int reminderId))
            {
                await RespondAsync("Invalid reminder selection.", ephemeral: true);
                return;
            }

            bool deleted = await DatabaseService.Instance.DeleteUserReminderAsync(Context.User.Id, reminderId);
            if (!deleted)
            {
                await RespondAsync("That reminder no longer exists or is already sent.", ephemeral: true);
                return;
            }

            if (Context.Interaction is not SocketMessageComponent component)
            {
                await RespondAsync($"Deleted reminder {reminderId}.", ephemeral: true);
                return;
            }

            List<DatabaseReminderModel> reminders = await DatabaseService.Instance.GetUserRemindersAsync(Context.User.Id);
            List<DatabaseReminderModel> activeReminders = reminders.Where(r => !r.HasSent).ToList();

            if (activeReminders.Count == 0)
            {
                await component.UpdateAsync(msg =>
                {
                    msg.Content = "📭 You don't have any active reminders.";
                    msg.Embed = null;
                    msg.Components = new ComponentBuilder().Build();
                });
                return;
            }

            (Embed embed, MessageComponent? components) = BuildRemindersMessage(Context.User, activeReminders);

            await component.UpdateAsync(msg =>
            {
                msg.Content = string.Empty;
                msg.Embed = embed;
                msg.Components = components ?? new ComponentBuilder().Build();
            });
        }

        private static (Embed Embed, MessageComponent? Components) BuildRemindersMessage(IUser user, List<DatabaseReminderModel> reminders)
        {
            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle($"📋 Your Reminders ({reminders.Count})")
                .WithColor(Color.Blue)
                .WithFooter($"Requested by {user}")
                .WithTimestamp(DateTime.UtcNow);

            if (reminders.Count > MaxDeleteOptions)
            {
                embed.WithDescription($"Showing the next {MaxDeleteOptions} reminders. Delete a few and re-run /myreminders to see the rest.");
            }

            foreach (DatabaseReminderModel reminder in reminders.OrderBy(r => r.ReminderTime).Take(MaxDeleteOptions))
            {
                long unix = new DateTimeOffset(reminder.ReminderTime).ToUnixTimeSeconds();
                string label = TruncateLabel(reminder.Task, MaxLabelLength);

                embed.AddField(
                    label,
                    $"ID: {reminder.Id}\n" +
                    $"<t:{unix}:F>\n" +
                    $"*<t:{unix}:R>*",
                    inline: false);
            }

            if (reminders.Count == 0)
                return (embed.Build(), null);

            SelectMenuBuilder menu = new SelectMenuBuilder()
                .WithCustomId("reminder-delete")
                .WithPlaceholder("Select a reminder to delete")
                .WithMinValues(1)
                .WithMaxValues(1);

            foreach (DatabaseReminderModel reminder in reminders.OrderBy(r => r.ReminderTime).Take(MaxDeleteOptions))
            {
                string label = TruncateLabel(reminder.Task, 100);
                menu.AddOption(label, reminder.Id.ToString(), $"ID {reminder.Id}");
            }

            MessageComponent components = new ComponentBuilder().WithSelectMenu(menu).Build();
            return (embed.Build(), components);
        }

        private static string TruncateLabel(string text, int maxLength)
        {
            string trimmed = string.IsNullOrWhiteSpace(text) ? "Reminder" : text.Trim();

            if (trimmed.Length <= maxLength)
                return trimmed;

            return trimmed[..(maxLength - 3)] + "...";
        }
    }
}
