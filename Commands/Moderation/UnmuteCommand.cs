using Discord.Interactions;
using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Commands.Moderation
{
    public sealed class UnmuteCommand : LoggedCommandModule
    {
        [SlashCommand("unmute", "Unmutes a member by removing the 'Muted' role")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.MuteMembers)]
        public async Task UnmuteAsync(
            [Summary("target", "The member to unmute")] IGuildUser target)
        {
            await LogCommandAsync(("target", target));
            await UnmuteUserAsync(target);
        }

        [UserCommand("Unmute User")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.MuteMembers)]
        public async Task UnmuteUserCmAsync(IGuildUser target)
        {
            await LogCommandAsync(("target", target));
            await UnmuteUserAsync(target);
        }

        [MessageCommand("Unmute Message Author")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.MuteMembers)]
        public async Task UnmuteMessageCmAsync(IMessage message)
        {
            if (message.Author is not IGuildUser target)
            {
                await RespondAsync("❌ Could not find the message author. They may have left the guild.", ephemeral: true);
                return;
            }

            await LogCommandAsync(("target", target));
            await UnmuteUserAsync(target);
        }
        private async Task UnmuteUserAsync(IGuildUser target)
        {
            await DeferAsync(ephemeral: true);

            SocketTextChannel? staffLog = Context.Guild.TextChannels.FirstOrDefault(channel => channel.Name == "staff-log");
            SocketRole? mutedRole = Context.Guild.Roles.FirstOrDefault(role => role.Name.Equals("muted", StringComparison.CurrentCultureIgnoreCase));

            if (mutedRole == null)
            {
                await FollowupAsync("❌ Could not find a role named 'Muted'.", ephemeral: true);
                return;
            }

            if (!target.RoleIds.Contains(mutedRole.Id))
            {
                await FollowupAsync("❌ This user is not muted.", ephemeral: true);
                return;
            }

            await target.RemoveRoleAsync(mutedRole);
            await FollowupAsync($"🔊 {target.Mention} has been unmuted.", ephemeral: true);

            if (staffLog != null)
            {
                string logMessage = $"🔊 **{target.Mention}** has been unmuted by **{Context.User.Mention}**.";
                await staffLog.SendMessageAsync(logMessage);
            }
        }
    }
}