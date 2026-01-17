using Discord.Interactions;
using tsgsBot_C_.Services;
using Discord.WebSocket;
using Discord.Net;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class VerifyCommand : LoggedCommandModule
    {
        [SlashCommand("verify", "Verify your donation to receive the exclusive supporter role")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task VerifyAsync([Summary("code", "Your unique verification code")] string code)
        {
            SocketRole? supportRole = Context.Guild.Roles.FirstOrDefault(role => role.Name.Equals("supporter", StringComparison.CurrentCultureIgnoreCase));
            await DeferAsync(ephemeral: true);

            string validCode = await DatabaseService.Instance.GetSecretAsync("supporter_verification_code");

            // 1. Log the command usage (with the code for audit/security)
            await LogCommandAsync(("code", code));

            // 2. Command logic
            SocketGuildUser guildUser = (SocketGuildUser)Context.User; // Safe cast (command is guild-only)

            string message;

            if (supportRole == null)
            {
                message = "The supporter role does not exist on this server. Please contact an admin.";
            }
            else if (code != validCode)
            {
                message = "Invalid verification code. Please check your code and try again.";
            }
            else if (guildUser.Roles.Any(r => r.Id == supportRole.Id))
            {
                message = "You already have the supporter role — thank you for your support! ❤️";
            }
            else
            {
                try
                {
                    await guildUser.AddRoleAsync(supportRole);
                    message = "Thank you for verifying your donation! You've been given the supporter role 🎉";
                }
                catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.MissingPermissions)
                {
                    message = "I couldn't assign the role — please contact an admin (likely a permissions or role hierarchy issue).";
                }
                catch (Exception ex)
                {
                    // Log unexpected errors
                    await LogCommandAsync(("error", ex.Message)); // optional extra log
                    message = "An unexpected error occurred while assigning the role. Please try again later or contact support.";
                }
            }

            // 3. Send ephemeral response
            await RespondAsync(message, ephemeral: true);
        }
    }
}