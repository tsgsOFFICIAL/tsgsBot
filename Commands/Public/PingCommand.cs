using Discord.Interactions;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class PingCommand : LoggedCommandModule
    {
        [SlashCommand("ping", "pong")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task PingAsync()
        {
            await LogCommandAsync();

            int latency = Context.Client.Latency;

            string message = $"Pong! {latency} ms";

            await RespondAsync(message, ephemeral: true);
        }
    }
}