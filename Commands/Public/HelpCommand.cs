using Discord.Interactions;
using Discord.WebSocket;
using System.Text;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class HelpCommand(InteractionService interactionService) : LoggedCommandModule
    {
        [SlashCommand("help", "Shows all commands you can use right now")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task HelpAsync()
        {
            await LogCommandAsync();

            // Get the current user & context
            SocketUser user = Context.User;
            SocketGuild? guild = Context.Guild;
            bool isInGuild = guild != null;

            // We'll collect lines here
            List<string> lines = new List<string>();
            lines.Add("**Available commands:**\n");

            // Get all registered slash commands
            List<SlashCommandInfo> allCommands = [.. interactionService.SlashCommands.OrderBy(c => c.Name)];

            foreach (SlashCommandInfo cmd in allCommands)
            {
                // 1. Check context compatibility
                bool allowedInContext = true;
                CommandContextTypeAttribute? contextTypeAttr = cmd.Attributes.OfType<CommandContextTypeAttribute>().FirstOrDefault();
                if (contextTypeAttr != null)
                {
                    // If the command is not enabled in DMs and we're not in a guild, skip
                    if (!isInGuild && !contextTypeAttr.ContextTypes.Contains(InteractionContextType.BotDm))
                    {
                        allowedInContext = false;
                    }
                    // If the command is not enabled in guilds and we are in a guild, skip
                    if (isInGuild && !contextTypeAttr.ContextTypes.Contains(InteractionContextType.Guild))
                    {
                        allowedInContext = false;
                    }
                }

                // 2. Check guild permissions (if in guild)
                bool hasPermission = true;
                if (isInGuild && cmd.DefaultMemberPermissions != null)
                {
                    GuildPermission required = cmd.DefaultMemberPermissions.Value;
                    SocketGuildUser member = guild!.GetUser(user.Id);
                    if (member != null)
                    {
                        hasPermission = member.GuildPermissions.Has(required);
                    }
                    else
                    {
                        // Rare case: user not cached → assume no permission
                        hasPermission = false;
                    }
                }

                if (!allowedInContext || !hasPermission)
                    continue;

                // Build command line
                StringBuilder sb = new StringBuilder();
                sb.Append($"**/{cmd.Name}**");

                // Parameters
                foreach (SlashCommandParameterInfo? param in cmd.Parameters)
                {
                    string brackets = param.IsRequired ? "**" : "*";
                    sb.Append($" {brackets}[{param.Name}]{brackets}");
                }

                sb.Append($"\n*{cmd.Description ?? "No description"}*");

                // Show required permissions
                string permsText = "None";
                if (cmd.DefaultMemberPermissions != null && cmd.DefaultMemberPermissions.Value != 0)
                {
                    permsText = string.Join(", ", new GuildPermissions((ulong)cmd.DefaultMemberPermissions.Value).GetPermissionNames());
                }
                sb.Append($"\n**Required Permissions:** {permsText}\n");

                lines.Add(sb.ToString());
            }

            if (lines.Count == 1) // only the header
            {
                await RespondAsync("You don't have access to any commands in this context.", ephemeral: true);
                return;
            }

            // Split into chunks ≤ 2000 chars
            List<string> messages = new List<string>();
            StringBuilder current = new StringBuilder();

            foreach (string line in lines)
            {
                if (current.Length + line.Length > 1900) // safety margin
                {
                    messages.Add(current.ToString());
                    current.Clear();
                }
                current.Append(line);
            }

            if (current.Length > 0)
                messages.Add(current.ToString());

            // Send as ephemeral follow-ups
            for (int i = 0; i < messages.Count; i++)
            {
                if (i == 0)
                {
                    await RespondAsync(messages[i], ephemeral: true);
                }
                else
                {
                    await FollowupAsync(messages[i], ephemeral: true);
                }
            }
        }
    }

    public static class GuildPermissionsExtensions
    {
        public static IEnumerable<string> GetPermissionNames(this GuildPermissions perms)
        {
            List<string> names = new List<string>();
            foreach (GuildPermission flag in Enum.GetValues<GuildPermission>())
            {
                if (perms.Has(flag))
                    names.Add(flag.ToString());
            }
            return names.OrderBy(n => n);
        }
    }
}