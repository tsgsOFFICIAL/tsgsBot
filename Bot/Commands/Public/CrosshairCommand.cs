using Discord.Interactions;
using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Bot.Commands.Public
{
    public sealed class CrosshairCommand : LoggedCommandModule
    {
        [SlashCommand("crosshair", "Get directions to download and install CrosshairY")]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public Task StreamDropCollectorLongAsync() => SendInfoEmbedAsync();

        [SlashCommand("crosshairy", "Get directions to download and install CrosshairY")]
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
                .WithTitle("Get CrosshairY")
                .WithDescription("Interested in **CrosshairY**? Grab it now and join the club!")
                .WithColor(new Color(252, 186, 3)) // #fcba03
                .WithUrl("https://github.com/tsgsOFFICIAL/CrosshairY#installation")
                .WithAuthor(
                    name: "CrosshairY",
                    iconUrl: "https://raw.githubusercontent.com/tsgsOFFICIAL/CrosshairY/refs/heads/master/assets/logo.png",
                    url: "https://github.com/tsgsOFFICIAL/CrosshairY#installation")
                .WithFooter(botTag, botAvatarUrl)
                .WithCurrentTimestamp()
                .WithThumbnailUrl("https://raw.githubusercontent.com/tsgsOFFICIAL/CrosshairY/refs/heads/master/assets/logo.png")
                .AddField("Download & Quick Start",
                    "[Click here to get started](<https://github.com/tsgsOFFICIAL/CrosshairY#installation>)",
                    inline: false);

            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}