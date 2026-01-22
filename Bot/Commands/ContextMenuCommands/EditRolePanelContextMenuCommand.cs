using tsgsBot_C_.StateServices;
using Discord.Interactions;
using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Bot.Commands.ContextMenuCommands
{
    /// <summary>
    /// Context menu command for editing existing role panel messages.
    /// Extracts role data from message buttons and opens the edit modal.
    /// </summary>
    public sealed class EditRolePanelContextMenuCommand(RolePanelFormStateService stateService, ILogger<EditRolePanelContextMenuCommand> logger) : LoggedCommandModule
    {
        private readonly RolePanelFormStateService _stateService = stateService;
        private readonly ILogger<EditRolePanelContextMenuCommand> _logger = logger;

        [MessageCommand("Edit Role Panel")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.ManageRoles)]
        public async Task EditRolePanelMessageAsync(IMessage targetMessage)
        {
            _logger.LogInformation("Edit role panel command invoked by user {UserId} on message {MessageId}", Context.User.Id, targetMessage.Id);

            if (targetMessage is not IUserMessage userMessage || userMessage.Embeds.Count == 0)
            {
                await RespondAsync("Target message must have an embed.", ephemeral: true);
                return;
            }

            try
            {
                // Try to extract roles from the buttons in the message
                List<ulong> roleIds = new();
                List<string> buttonLabels = new();

                if (userMessage.Components != null)
                {
                    foreach (IMessageComponent? row in userMessage.Components)
                    {
                        if (row is ActionRowComponent actionRow)
                        {
                            foreach (IMessageComponent? component in actionRow.Components)
                            {
                                if (component is ButtonComponent button && button.CustomId?.StartsWith("rolepanel-toggle:") == true)
                                {
                                    string idStr = button.CustomId.Substring("rolepanel-toggle:".Length);
                                    if (ulong.TryParse(idStr, out ulong roleId))
                                    {
                                        roleIds.Add(roleId);
                                        buttonLabels.Add(button.Label ?? "");
                                    }
                                }
                            }
                        }
                    }
                }

                if (roleIds.Count == 0)
                {
                    _logger.LogWarning("No role buttons found in message {MessageId}", targetMessage.Id);
                    await RespondAsync("Message doesn't appear to be a valid role panel (no role buttons found).", ephemeral: true);
                    return;
                }

                SocketGuild guild = Context.Guild!;
                List<IRole> roles = ResolveRoles(guild, roleIds);

                if (roles.Count != roleIds.Count)
                {
                    _logger.LogWarning("Message {MessageId}: {MissingCount} roles are no longer available", targetMessage.Id, roleIds.Count - roles.Count);
                }

                // Extract embed data
                IEmbed messageEmbed = userMessage.Embeds.First();
                string title = messageEmbed.Title ?? "Self-assign Roles";
                string description = messageEmbed.Description ?? "";

                // Create state from the message
                UserRolePanelFormState state = _stateService.GetOrCreate(Context.User.Id);
                state.Title = title;
                state.Description = description;
                state.RoleIds = roles.Select(r => r.Id).ToList();
                state.SkippedRoleIds = new();
                state.ButtonLabels = buttonLabels;
                state.ImageUrl = messageEmbed.Image?.Url;
                state.OriginalMessageId = targetMessage.Id;

                _logger.LogInformation("Loaded role panel state for editing: title='{Title}', roles={RoleCount}", title, roles.Count);

                // Open edit modal directly instead of showing preview
                try
                {
                    ModalBuilder modal = new ModalBuilder()
                        .WithCustomId("rolepanel-edit-modal")
                        .WithTitle("Edit Role Panel")
                        .AddTextInput("Title", "title", TextInputStyle.Short, placeholder: "Self-assign Roles", value: state.Title, required: true)
                        .AddTextInput("Description", "description", TextInputStyle.Paragraph, placeholder: "Select your roles below", value: state.Description, required: true)
                        .AddTextInput("Button Labels (one per line)", "button_labels", TextInputStyle.Paragraph,
                            placeholder: "Role 1\nRole 2\nRole 3", value: string.Join("\n", state.ButtonLabels), required: false)
                        .AddFileUpload("Image (optional)", "image", 0, 1, false);

                    await RespondWithModalAsync(modal.Build());
                    _logger.LogInformation("Edit modal opened for message {MessageId}", targetMessage.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to open edit modal for message {MessageId}", targetMessage.Id);
                    await RespondAsync("Failed to open edit modal. Please try again.", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading role panel for editing from message {MessageId}", targetMessage.Id);
                await RespondAsync("Failed to load panel data. Ensure it's a valid role panel.", ephemeral: true);
            }
        }

        private static List<IRole> ResolveRoles(SocketGuild guild, IEnumerable<ulong> roleIds) =>
            roleIds.Select(id => guild.GetRole(id)).Where(r => r != null).Cast<IRole>().OrderByDescending(r => r.Position).ToList();
    }
}