using Discord.Interactions;
using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class UserInfoCommand : LoggedCommandModule
    {
        [SlashCommand("userinfo", "Get user info")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task UserInfoAsync([Summary("user", "The user to show information on")] IUser targetUser)
        {
            // 1. Log the command usage
            await LogCommandAsync(("targetUserId", targetUser));

            // 2. Try to get guild member info (from current guild if possible)
            SocketGuildUser? member = null;
            if (Context.Guild != null)
            {
                member = Context.Guild.GetUser(targetUser.Id);
            }

            string displayName = (Context.User as SocketGuildUser)?.Nickname ?? Context.User.Username;

            // 3. Build embed
            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle($"{targetUser.Username}'s Userinfo")
                .WithDescription($"Showing information about <@{targetUser.Id}>")
                .WithColor(new Color(252, 186, 3)) // #fcba03 orange-ish
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithUrl("https://discord.gg/Cddu5aJ")
                .WithAuthor(displayName, targetUser.GetAvatarUrl(size: 512), "https://discord.gg/Cddu5aJ")
                .WithFooter(Context.Client.CurrentUser?.Username ?? "Bot", Context.Client.CurrentUser?.GetAvatarUrl(size: 512))
                .WithThumbnailUrl(targetUser.GetAvatarUrl(size: 512) ?? targetUser.GetDefaultAvatarUrl())
                .AddField("Username", targetUser.Username, inline: true)
                .AddField("Server member since",
                    member != null
                        ? $"<t:{member.JoinedAt?.ToUnixTimeSeconds()}:F>"
                        : "Not in server",
                    inline: true)
                .AddField("Account created", $"<t:{targetUser.CreatedAt.ToUnixTimeSeconds()}:F>", inline: true)
                .AddField("Bot?", targetUser.IsBot ? "Yes" : "No", inline: true);

            // Roles field
            string rolesText = "Not in server";
            if (member != null)
            {
                if (member.Roles.Any())
                {
                    ulong? everyoneRoleId = Context.Guild?.EveryoneRole.Id;
                    rolesText = string.Join(", ", member.Roles
                        .Where(role => everyoneRoleId == null || role.Id != everyoneRoleId.Value) // skip @everyone
                        .Select(role => $"<@&{role.Id}>"));
                }
                else
                {
                    rolesText = "None";
                }
            }
            embed.AddField("Roles", string.IsNullOrEmpty(rolesText) ? "None" : rolesText, inline: false);

            // 4. Send ephemeral embed response
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}