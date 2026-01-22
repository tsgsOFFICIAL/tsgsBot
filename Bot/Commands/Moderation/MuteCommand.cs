using Discord.Interactions;
using Discord.WebSocket;
using tsgsBot_C_.Utils;
using Discord;

namespace tsgsBot_C_.Bot.Commands.Moderation
{
    public sealed class MuteCommands : LoggedCommandModule
    {
        [SlashCommand("mute", "Mutes a member by giving them the 'Muted' role")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.MuteMembers)]
        public async Task MuteAsync(
            [Summary("target", "The member to mute")] IGuildUser target,
            [Summary("duration", "Optional mute duration (e.g. 7h 30m, 2d, 1w)")] string? duration = null,
            [Summary("reason", "Optional reason for muting")] string? reason = null)
        {
            reason ??= "No reason provided";

            await LogCommandAsync(("target", target), ("duration", duration), ("reason", reason));

            await MuteUserAsync(target, duration, reason);
        }

        [ModalInteraction("mute_modal_*", TreatAsRegex = true)]
        public async Task HandleMuteModalAsync(MuteModal modal)
        {
            await DeferAsync(ephemeral: true);

            // Parse user ID from CustomId (e.g., "mute_modal_123456789")
            string[]? customIdParts = (Context.Interaction as IModalInteraction)?.Data.CustomId.Split('_');
            if (customIdParts == null || customIdParts.Length < 3 || !ulong.TryParse(customIdParts[2], out ulong userId))
            {
                await FollowupAsync("❌ Invalid modal data.", ephemeral: true);
                return;
            }

            SocketGuildUser target = Context.Guild.GetUser(userId);
            if (target == null)
            {
                await FollowupAsync("❌ Could not find the target user. They may have left the guild.", ephemeral: true);
                return;
            }

            string? duration = string.IsNullOrWhiteSpace(modal.Duration) ? null : modal.Duration.Trim();
            string reason = string.IsNullOrWhiteSpace(modal.Reason) ? "No reason provided" : modal.Reason.Trim();

            await MuteUserAsync(target, duration, reason, defer: false); // Already deferred
        }

        private async Task MuteUserAsync(IGuildUser target, string? duration, string reason, bool defer = true)
        {
            if (defer)
                await DeferAsync(ephemeral: true);

            SocketTextChannel? staffLog = Context.Guild.TextChannels.FirstOrDefault(channel => channel.Name == "staff-log");
            SocketRole? mutedRole = Context.Guild.Roles.FirstOrDefault(role => role.Name.Equals("muted", StringComparison.CurrentCultureIgnoreCase));

            if (mutedRole == null)
            {
                await FollowupAsync("❌ Could not find a role named 'Muted'. Please create one.", ephemeral: true);
                return;
            }

            if (!Context.Guild.CurrentUser.GuildPermissions.ManageRoles || target.Hierarchy >= Context.Guild.CurrentUser.Hierarchy)
            {
                await FollowupAsync("❌ I can't mute this user. They might have a higher role or I don't have permission.", ephemeral: true);
                return;
            }

            await target.AddRoleAsync(mutedRole);

            TimeSpan? muteDuration = duration != null
                ? HelperMethods.ParseDuration(duration)
                : null;

            string durationText = muteDuration != null
                ? $" for {duration}"
                : "";

            await FollowupAsync(
                $"🔇 {target.Mention} has been muted{durationText}. Reason: {reason}",
                ephemeral: true);

            if (staffLog != null)
            {
                string logMessage =
                    $"🔇 **{target.Mention}** has been muted by **{Context.User.Mention}**" +
                    $"{durationText}. Reason: {reason}";

                await staffLog.SendMessageAsync(logMessage);
            }

            // Timed unmute (non-persistent)
            if (muteDuration is { } delay && delay > TimeSpan.Zero)
            {
                _ = Task.Delay(delay).ContinueWith(async _ =>
                {
                    try
                    {
                        if (target.RoleIds.Contains(mutedRole.Id))
                        {
                            await target.RemoveRoleAsync(mutedRole);
                        }
                    }
                    catch
                    {
                        // Ignore (user left, role deleted, bot restarted, etc.)
                    }
                });
            }
        }
    }

    public class MuteModal : IModal
    {
        public string Title => "Mute User"; // Overridden in builder

        [InputLabel("Duration (e.g. 7h 30m, 2d, 1w)")]
        [ModalTextInput("duration", TextInputStyle.Short)]
        public required string Duration { get; set; }

        [InputLabel("Reason for mute")]
        [ModalTextInput("reason", TextInputStyle.Paragraph)]
        public required string Reason { get; set; }
    }
}