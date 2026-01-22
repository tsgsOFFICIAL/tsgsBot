using Discord.Interactions;
using Discord;

namespace tsgsBot_C_.Bot.Commands.ContextMenuCommands
{
    /// <summary>
    /// Context menu commands for muting users via right-click menu.
    /// </summary>
    public sealed class MuteContextMenuCommand : LoggedCommandModule
    {
        [UserCommand("Mute User")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.MuteMembers)]
        public async Task MuteUserCmAsync(IGuildUser target)
        {
            await LogCommandAsync(("target", target));
            await ShowMuteModalAsync(target);
        }

        [MessageCommand("Mute Message Author")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.MuteMembers)]
        public async Task MuteMessageCmAsync(IMessage message)
        {
            if (message.Author is not IGuildUser target)
            {
                await RespondAsync("❌ Could not find the message author. They may have left the guild.", ephemeral: true);
                return;
            }

            await LogCommandAsync(("target", target));
            await ShowMuteModalAsync(target);
        }

        private async Task ShowMuteModalAsync(IGuildUser target)
        {
            ModalBuilder modal = new ModalBuilder()
                .WithTitle($"Mute {target.Username}")
                .WithCustomId($"mute_modal_{target.Id}")
                .AddTextInput("Duration (e.g., 10m, 1h, 2d)", "duration", TextInputStyle.Short, required: false)
                .AddTextInput("Reason for mute", "reason", TextInputStyle.Paragraph, required: false);

            await RespondWithModalAsync(modal.Build());
        }
    }
}