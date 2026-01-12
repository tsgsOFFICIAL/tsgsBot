using Discord.Interactions;
using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Commands.ContextMenuCommands
{
    [Group("context", "Context menu commands")]
    public sealed class ContextMenuCommands : LoggedCommandModule
    {
        private const ulong ReportsChannelId = 690284349521788940;

        [MessageCommand("Report Message")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task ReportMessageAsync(IMessage message)
        {
            await LogCommandAsync(("messageId", message.Id), ("messageAuthorId", message.Author.Id));
            await DeferAsync(ephemeral: true);

            SocketUser reporter = Context.User;

            if (Context.Client.GetChannel(ReportsChannelId) is not IMessageChannel channel)
            {
                await FollowupAsync("⚠️ Couldn't find the reports channel.", ephemeral: true);
                return;
            }

            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle("🚨 Message Report")
                .WithColor(Color.Red)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .AddField("Reporter", reporter.Mention, inline: true)
                .AddField("Author", message.Author.Mention, inline: true)
                .AddField(
                    "Channel",
                    Context.Channel is IGuildChannel guildChannel
                        ? $"<#{guildChannel.Id}>"
                        : "Unknown",
                    inline: true)
                .AddField("Message Content", string.IsNullOrWhiteSpace(message.Content) ? "*No content*" : message.Content, inline: false)
                .AddField("Message Link", $"[Jump to message]({message.GetJumpUrl()})", inline: false);

            if (message.Attachments.Any())
            {
                embed.AddField("Attachments", string.Join("\n", message.Attachments.Select(a => a.Url)), inline: false);
            }

            await channel.SendMessageAsync(embed: embed.Build());

            await FollowupAsync("✅ The message has been reported to the moderators.", ephemeral: true);
        }
    }
}