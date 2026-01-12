using Discord.Interactions;
using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class StreamDropCollectorCommand : LoggedCommandModule
    {
        [SlashCommand("streamdropcollector", "Get directions to download and install the StreamDropCollector program")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public Task StreamDropCollectorLongAsync() => SendInfoEmbedAsync();

        [SlashCommand("sdc", "Get directions to download and install StreamDropCollector (short)")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public Task StreamDropCollectorShortAsync() => SendInfoEmbedAsync();

        private async Task SendInfoEmbedAsync()
        {
            // Log once
            await LogCommandAsync();

            // Bot info for footer
            SocketSelfUser? botUser = Context.Client.CurrentUser;
            string botTag = botUser != null ? botUser.Username : "Bot";
            string? botAvatarUrl = botUser?.GetAvatarUrl(ImageFormat.Png, 128);

            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle("Get Stream Drop Collector")
                .WithDescription("Interested in **StreamDropCollector**? Grab it now and join the club!")
                .WithColor(new Color(252, 186, 3)) // #fcba03
                .WithUrl("https://github.com/tsgsOFFICIAL/StreamDropCollector?tab=readme-ov-file#quick-start")
                .WithAuthor(
                    name: "StreamDropCollector",
                    iconUrl: "https://raw.githubusercontent.com/tsgsOFFICIAL/StreamDropCollector/refs/heads/master/UI/Assets/logo.png",
                    url: "https://github.com/tsgsOFFICIAL/StreamDropCollector?tab=readme-ov-file#quick-start")
                .WithFooter(botTag, botAvatarUrl)
                .WithCurrentTimestamp()
                .WithThumbnailUrl("https://raw.githubusercontent.com/tsgsOFFICIAL/StreamDropCollector/refs/heads/master/UI/Assets/logo.png")
                .AddField("Download & Quick Start",
                    "[Click here to get started](<https://github.com/tsgsOFFICIAL/StreamDropCollector?tab=readme-ov-file#quick-start>)",
                    inline: false);

            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}