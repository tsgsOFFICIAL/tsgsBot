using Discord.Interactions;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class PingCommand : LoggedCommandModule
    {
        [SlashCommand("ping", "Check the bot's latency.")]
        [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task PingAsync()
        {
            await LogCommandAsync();

            // Defer to give the bot time to respond
            await DeferAsync(ephemeral: true);

            // Bot -> Discord latency
            int discordLatency = Context.Client.Latency;

            // Bot -> User latency = now - interaction created time
            TimeSpan userLatency = DateTimeOffset.UtcNow - Context.Interaction.CreatedAt;

            // Send final message
            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = $"🏓 Pong!\n" +
                              $"**Latency (Bot → Discord):** {discordLatency} ms\n" +
                              $"**Latency (Bot → You):** {userLatency.TotalMilliseconds:F0} ms";
            });
        }
    }
}
