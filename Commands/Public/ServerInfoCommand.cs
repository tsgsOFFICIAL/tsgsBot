using Discord.Interactions;
using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class ServerInfoCommand : LoggedCommandModule
    {
        [SlashCommand("serverinfo", "Displays information about the server")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task ServerInfoAsync()
        {
            // 1. Log usage
            await LogCommandAsync();

            // 2. Get current guild (safe because command is guild-only)
            if (Context.Guild is not SocketGuild guild)
            {
                await RespondAsync("This command can only be used in a server.", ephemeral: true);
                return;
            }

            // 3. Fetch owner (from cache or API)
            SocketGuildUser? owner = guild.GetUser(guild.OwnerId);

            // 4. Icon URL
            string? iconUrl = guild.IconUrl;

            // 5. Member counts (approximate from gateway cache)
            int totalMembers = guild.MemberCount;
            int botCount = guild.Users.Count(u => u.IsBot);
            int humanCount = totalMembers - botCount;

            // 6. Bot footer info
            SocketSelfUser? botUser = Context.Client.CurrentUser;
            string botTag = botUser?.Username ?? "Bot";
            string? botAvatarUrl = botUser?.GetAvatarUrl(ImageFormat.Png, 128);

            // 7. Build embed
            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle($"{guild.Name}'s Information")
                .WithDescription($"Showing information about **{guild.Name}**")
                .WithColor(new Color(252, 186, 3)) // #fcba03
                .WithUrl("https://discord.gg/Cddu5aJ")
                .WithAuthor(
                    name: owner?.Nickname ?? owner?.Username,
                    iconUrl: owner?.GetAvatarUrl(ImageFormat.Auto, 128),
                    url: "https://discord.gg/Cddu5aJ")
                .WithFooter(botTag, botAvatarUrl)
                .WithCurrentTimestamp()
                .WithThumbnailUrl(iconUrl)  // Using thumbnail instead of full image (looks better)
                .AddField("Server Name", guild.Name, inline: true)
                .AddField("Member Count", humanCount.ToString("N0"), inline: true)
                .AddField("Bot Count", botCount.ToString("N0"), inline: true)
                .AddField("Owner", owner != null ? owner.Mention : "Unknown", inline: true)
                .AddField("Created On", $"<t:{guild.CreatedAt.ToUnixTimeSeconds()}:F>", inline: false)
                .AddField("Boost Level", guild.PremiumTier.ToString(), inline: true)
                .AddField("Boost Count", guild.PremiumSubscriptionCount.ToString(), inline: true);

            // 8. Respond ephemeral
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}