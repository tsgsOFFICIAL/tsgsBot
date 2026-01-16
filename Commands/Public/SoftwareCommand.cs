using Discord.Interactions;
using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class SoftwareCommand : LoggedCommandModule
    {
        [SlashCommand("software", "Get links to download and install tsgsOFFICIAL's software")]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task SoftwareAsync()
        {
            // Log command usage
            await LogCommandAsync();

            // Bot footer info
            SocketSelfUser? botUser = Context.Client.CurrentUser;
            string botTag = botUser?.Username ?? "Bot";
            string? botAvatarUrl = botUser?.GetAvatarUrl(ImageFormat.Png, 128);

            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle("Get my free software here")
                .WithDescription("Interested in any of my free shiz? See what's up, and grab your free copies!")
                .WithColor(new Color(252, 186, 3)) // #fcba03
                .WithUrl("https://github.com/tsgsOFFICIAL")
                .WithAuthor(
                    name: "tsgsOFFICIAL",
                    iconUrl: "https://avatars.githubusercontent.com/u/29740481",
                    url: "https://github.com/tsgsOFFICIAL")
                .WithFooter(botTag, botAvatarUrl)
                .WithCurrentTimestamp()
                .WithImageUrl("https://avatars.githubusercontent.com/u/29740481")
                .AddField("StreamDropCollector",
                    "Automatically earns Kick & Twitch drops while you’re AFK\n" +
                    "[Download →](<https://github.com/tsgsOFFICIAL/StreamDropCollector?tab=readme-ov-file#quick-start>)",
                    inline: true)
                .AddField("CS2 AutoAccept",
                    "Automatically accepts CS2 and Faceit matches\n" +
                    "[Download →](<https://github.com/tsgsOFFICIAL/CS2-AutoAccept?tab=readme-ov-file#installation>)",
                    inline: true)
                .AddField("Rusty Painter",
                    "Paints images onto signs, pumpkins, and more in Rust\n" +
                    "*Available soon!*",
                    inline: true)
                .AddField("Crosshair Y",
                    "The greatest free CrosshairX Alternative\n" +
                    "[Download →](<https://github.com/tsgsOFFICIAL/CrosshairY>)",
                    inline: true)
                .AddField("More Software",
                    "[Check out my GitHub for more!](<https://github.com/tsgsOFFICIAL>)",
                    inline: false);

            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}