using Discord.Interactions;
using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class ReportUserCommand : LoggedCommandModule
    {
        private const ulong ReportsChannelId = 690284349521788940UL;

        [SlashCommand("report", "Report a user to the moderators")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task ReportUserAsync(
            [Summary("user", "The user to report")] IUser targetUser,
            [Summary("reason", "What has this person done?")] string reason)
        {
            await LogCommandAsync(("target", targetUser.Id), ("reason", reason));

            SocketUser reporter = Context.User;

            if (Context.Client.GetChannel(ReportsChannelId) is not IMessageChannel reportChannel)
            {
                await RespondAsync("⚠️ Reports channel not found.", ephemeral: true);
                return;
            }

            string reportText = $"<@{reporter.Id}> reported <@{targetUser.Id}> for:\n**{reason}**";

            try
            {
                await reportChannel.SendMessageAsync(reportText);
                await RespondAsync($"Successfully reported <@{targetUser.Id}> for \"{reason}\"!", ephemeral: true);
            }
            catch
            {
                await RespondAsync("Failed to send report (missing permissions in reports channel?).", ephemeral: true);
            }
        }
    }
}