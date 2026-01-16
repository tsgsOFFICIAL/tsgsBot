using System.Text.RegularExpressions;
using Discord.Interactions;
using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Commands.Moderation
{
    public sealed class MuteCommands : LoggedCommandModule
    {
        [SlashCommand("mute", "Mutes a member by giving them the 'Muted' role")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.MuteMembers)]
        public async Task MuteAsync(
            [Summary("target", "The member to mute")] IGuildUser target,
            [Summary("duration", "Optional mute duration (e.g., 10m, 1h, 2d)")] string? duration = null,
            [Summary("reason", "Optional reason for muting")] string? reason = null)
        {
            reason ??= "No reason provided";

            await LogCommandAsync(("target", target), ("duration", duration), ("reason", reason));

            await MuteUserAsync(target, duration, reason);
        }

        [UserCommand("Mute User")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.MuteMembers)]
        public async Task MuteUserCmAsync(IGuildUser target)
        {
            await LogCommandAsync(("target", target));
            await ShowMuteModalAsync(target);
        }

        [MessageCommand("Mute Message Author")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.MuteMembers)]
        public async Task MuteMessageCmAsync(IMessage message)
        {
            if (message.Author is not IGuildUser target)
            {
                await RespondAsync("❌ Could not find the message author. They may have left the guild.", ephemeral: true);
                return;
            }

            await LogCommandAsync(("target", target));
            await ShowMuteModalAsync(target);
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

        private async Task ShowMuteModalAsync(IGuildUser target)
        {
            ModalBuilder modal = new ModalBuilder()
                .WithTitle($"Mute {target.Username}")
                .WithCustomId($"mute_modal_{target.Id}")
                .AddTextInput("Duration (e.g., 10m, 1h, 2d)", "duration", TextInputStyle.Short, required: false)
                .AddTextInput("Reason for mute", "reason", TextInputStyle.Paragraph, required: false);

            await RespondWithModalAsync(modal.Build());
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

            string durationText = duration != null ? $" for {duration}" : "";
            await FollowupAsync($"🔇 {target.Mention} has been muted{durationText}. Reason: {reason}", ephemeral: true);

            if (staffLog != null)
            {
                string logMessage = $"🔇 **{target.Mention}** has been muted by **{Context.User.Mention}**{durationText}. Reason: {reason}";
                await staffLog.SendMessageAsync(logMessage);
            }

            long? ms = ParseDuration(duration);
            if (ms.HasValue)
            {
                _ = Task.Delay((int)ms.Value).ContinueWith(async _ =>
                {
                    try
                    {
                        if (target.RoleIds.Contains(mutedRole.Id))
                        {
                            await target.RemoveRoleAsync(mutedRole);
                        }
                    }
                    catch { } // Ignore errors (e.g., user left)
                });
            }
        }

        private static long? ParseDuration(string? duration)
        {
            if (string.IsNullOrWhiteSpace(duration))
                return null;

            Match match = Regex.Match(duration, @"^(\d+)([smhd])$", RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;

            if (!long.TryParse(match.Groups[1].Value, out long val))
                return null;

            return match.Groups[2].Value.ToLower() switch
            {
                "s" => val * 1000,
                "m" => val * 60 * 1000,
                "h" => val * 3600 * 1000,
                "d" => val * 86400 * 1000,
                _ => null
            };
        }
    }

    public class MuteModal : IModal
    {
        public string Title => "Mute User"; // Overridden in builder

        [InputLabel("Duration (e.g., 10m, 1h, 2d)")]
        [ModalTextInput("duration", TextInputStyle.Short)]
        public required string Duration { get; set; }

        [InputLabel("Reason for mute")]
        [ModalTextInput("reason", TextInputStyle.Paragraph)]
        public required string Reason { get; set; }
    }
}