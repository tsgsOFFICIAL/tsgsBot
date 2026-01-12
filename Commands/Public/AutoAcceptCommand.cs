using Discord.Interactions;
using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class AutoAcceptCommand : LoggedCommandModule
    {
        [SlashCommand("autoaccept", "Get directions to download and install the AutoAccept program for CS2")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task AutoAcceptAsync()
        {
            // Log once
            await LogCommandAsync();

            // Bot info for footer
            SocketSelfUser? botUser = Context.Client.CurrentUser;
            string botTag = botUser != null ? botUser.Username : "Bot";
            string? botAvatarUrl = botUser?.GetAvatarUrl(ImageFormat.Png, 128);

            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle("Get Auto Accept for CS2")
                .WithDescription("Interested in **CS2 AutoAccept**? Get it now, join the club!")
                .WithColor(new Color(252, 186, 3)) // #fcba03
                .WithUrl("https://github.com/tsgsOFFICIAL/CS2-AutoAccept?tab=readme-ov-file#installation")
                .WithAuthor(
                    name: "CS2 AutoAccept",
                    iconUrl: "https://raw.githubusercontent.com/tsgsOFFICIAL/CS2-AutoAccept/refs/heads/main/CS2-AutoAccept/logo.png",
                    url: "https://github.com/tsgsOFFICIAL/CS2-AutoAccept?tab=readme-ov-file#installation")
                .WithFooter(botTag, botAvatarUrl)
                .WithCurrentTimestamp()
                .WithThumbnailUrl("https://raw.githubusercontent.com/tsgsOFFICIAL/CS2-AutoAccept/refs/heads/main/CS2-AutoAccept/logo.png")
                .AddField("Download & Quick Start",
                    "[Click here to get started](<https://github.com/tsgsOFFICIAL/CS2-AutoAccept?tab=readme-ov-file#installation>)",
                    inline: false);

            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}