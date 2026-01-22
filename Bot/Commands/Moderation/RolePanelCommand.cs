using tsgsBot_C_.StateServices;
using Discord.Interactions;
using Discord.WebSocket;
using tsgsBot_C_.Models;
using Discord.Rest;
using Discord;

namespace tsgsBot_C_.Bot.Commands.Moderation
{
    /// <summary>
    /// Slash command handler for creating and managing role panel embeds with self-assign buttons.
    /// Implements a preview/confirm/edit/cancel workflow with session state management.
    /// </summary>
    public sealed class RolePanelCommand(RolePanelFormStateService stateService, ILogger<RolePanelCommand> logger) : LoggedCommandModule
    {
        [SlashCommand("role-panel", "Create an embed with self-assign role buttons")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.ManageRoles)]
        public async Task CreateRolePanelAsync(
            [Summary("title", "Embed title")] string title,
            [Summary("description", "Embed description")] string description,
            [Summary("role1", "First role button")] IRole role1,
            [Summary("role2", "Second role (optional)")] IRole? role2 = null,
            [Summary("role3", "Third role (optional)")] IRole? role3 = null,
            [Summary("role4", "Fourth role (optional)")] IRole? role4 = null,
            [Summary("role5", "Fifth role (optional)")] IRole? role5 = null)
        {
            await DeferAsync(ephemeral: true);
            await LogCommandAsync(("title", title), ("description", description), ("roles", string.Join(", ", new[] { role1, role2, role3, role4, role5 }.Where(r => r != null).Select(r => r!.Name))));

            SocketGuild guild = Context.Guild!;
            SocketGuildUser botUser = guild.CurrentUser;

            List<IRole> roles = new() { role1 };
            if (role2 != null) roles.Add(role2);
            if (role3 != null) roles.Add(role3);
            if (role4 != null) roles.Add(role4);
            if (role5 != null) roles.Add(role5);

            roles = roles
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .OrderByDescending(r => r.Position)
                .ToList();

            List<IRole> manageableRoles = new();
            List<IRole> skippedRoles = new();

            foreach (IRole role in roles)
            {
                if (role.IsManaged || !botUser.GuildPermissions.ManageRoles || botUser.Hierarchy <= role.Position)
                {
                    skippedRoles.Add(role);
                    continue;
                }

                manageableRoles.Add(role);
            }

            if (manageableRoles.Count == 0)
            {
                logger.LogWarning("Role panel create failed: bot cannot manage any provided roles. Bot hierarchy {BotHierarchy}", botUser.Hierarchy);
                await FollowupAsync("I can't manage any of the provided roles. Move my role above them and try again.", ephemeral: true);
                return;
            }

            string sanitizedTitle = string.IsNullOrWhiteSpace(title) ? "Self-assign Roles" : title.Trim();
            string sanitizedDescription = string.IsNullOrWhiteSpace(description) ? "Click a button to toggle a role." : description.Trim().Replace("\\n", "\n");

            // Default button labels = role names
            List<string> buttonLabels = manageableRoles.Select(r => r.Name).ToList();

            UserRolePanelFormState state = stateService.GetOrCreate(Context.User.Id);
            state.Title = sanitizedTitle;
            state.Description = sanitizedDescription;
            state.RoleIds = manageableRoles.Select(r => r.Id).ToList();
            state.SkippedRoleIds = skippedRoles.Select(r => r.Id).ToList();
            state.ButtonLabels = buttonLabels;
            state.ImageUrl = null;
            state.OriginalMessageId = null;

            await ShowPreviewAsync(state, "Does this look good?", includeSkipped: true);

            logger.LogInformation("Role panel preview created by {UserId} with {RoleCount} manageable roles and {SkippedCount} skipped", Context.User.Id, manageableRoles.Count, skippedRoles.Count);
        }

        [ComponentInteraction("rolepanel-confirm")]
        public async Task HandleConfirmAsync()
        {
            await DeferAsync(ephemeral: true);

            if (!stateService.TryGet(Context.User.Id, out UserRolePanelFormState? state))
            {
                await FollowupAsync("Session expired. Run /role-panel again.", ephemeral: true);
                return;
            }

            SocketGuild guild = Context.Guild!;

            List<IRole> roles = ResolveRoles(guild, state!.RoleIds);
            if (roles.Count == 0)
            {
                await FollowupAsync("No valid roles to post. Please recreate the panel.", ephemeral: true);
                stateService.Clear(Context.User.Id);
                return;
            }

            ComponentBuilder buttons = BuildRoleButtons(state!, roles);

            string displayName = (Context.User as SocketGuildUser)?.Nickname ?? Context.User.Username;
            string avatarUrl = Context.User.GetAvatarUrl(size: 512);

            Embed embed = BuildEmbed(state!, displayName, avatarUrl, null);

            RestUserMessage sentMessage = await Context.Channel.SendMessageAsync(embed: embed, components: buttons.Build());

            // If editing an existing message, delete the old one
            if (state!.OriginalMessageId.HasValue)
            {
                try
                {
                    if (await Context.Channel.GetMessageAsync(state.OriginalMessageId.Value) is IMessage oldMessage)
                    {
                        await oldMessage.DeleteAsync();
                        logger.LogInformation("Deleted original role panel message {OldMessageId} after editing", state.OriginalMessageId.Value);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete original role panel message {OldMessageId}", state.OriginalMessageId.Value);
                }
            }

            string followup = "Role panel posted.";
            if (state!.SkippedRoleIds.Count > 0)
            {
                List<string> skippedNames = state!.SkippedRoleIds.Select(id => guild.GetRole(id)?.Name).Where(n => n != null).Cast<string>().ToList();
                if (skippedNames.Count > 0)
                    followup += $" Skipped: {string.Join(", ", skippedNames)}.";
            }

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = followup;
                msg.Embed = null;
                msg.Components = new ComponentBuilder().Build();
                msg.Flags = MessageFlags.Ephemeral;
            });

            stateService.Clear(Context.User.Id);
            logger.LogInformation("Role panel posted by {UserId} with {RoleCount} buttons, message ID {MessageId}", Context.User.Id, roles.Count, sentMessage.Id);
        }

        [ComponentInteraction("rolepanel-edit")]
        public async Task HandleEditAsync()
        {
            logger.LogInformation("Edit button clicked by user {UserId}", Context.User.Id);

            if (!stateService.TryGet(Context.User.Id, out UserRolePanelFormState? state))
            {
                logger.LogWarning("Edit clicked but no state found for user {UserId}", Context.User.Id);
                await RespondAsync("Session expired. Run /role-panel again.", ephemeral: true);
                return;
            }

            logger.LogInformation("Building edit modal for user {UserId} with {RoleCount} roles", Context.User.Id, state!.RoleIds.Count);

            try
            {
                ModalBuilder modal = new ModalBuilder()
                    .WithCustomId("rolepanel-edit-modal")
                    .WithTitle("Edit Role Panel")
                    .AddTextInput("Title", "title", TextInputStyle.Short, placeholder: "Self-assign Roles", value: state!.Title, required: true)
                    .AddTextInput("Description", "description", TextInputStyle.Paragraph, placeholder: "Select your roles below", value: state!.Description, required: true)
                    .AddTextInput("Button Labels (one per line)", "button_labels", TextInputStyle.Paragraph,
                        placeholder: "Role 1\nRole 2\nRole 3", value: string.Join("\n", state!.ButtonLabels), required: false)
                    .AddFileUpload("Image (optional)", "image", 0, 1, false);

                await RespondWithModalAsync(modal.Build());
                logger.LogInformation("Modal shown successfully to user {UserId}", Context.User.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to show edit modal for user {UserId}", Context.User.Id);
                await RespondAsync("Failed to open edit modal. Please try again.", ephemeral: true);
            }
        }

        [ModalInteraction("rolepanel-edit-modal")]
        public async Task HandleEditModalAsync(RolePanelModalModel modal)
        {
            logger.LogInformation("Edit modal submitted by user {UserId}", Context.User.Id);
            await DeferAsync(ephemeral: true);

            if (!stateService.TryGet(Context.User.Id, out UserRolePanelFormState? state))
            {
                logger.LogWarning("Modal submitted but no state found for user {UserId}", Context.User.Id);
                await FollowupAsync("Session expired. Run /role-panel again.", ephemeral: true);
                return;
            }

            // Get uploaded image if present
            string? uploadedImageUrl = state!.ImageUrl;
            if (Context.Interaction is SocketModal socketModal && socketModal.Data.Attachments != null && socketModal.Data.Attachments.Count() > 0)
            {
                uploadedImageUrl = socketModal.Data.Attachments.First().Url;
            }

            string newTitle = string.IsNullOrWhiteSpace(modal.PanelTitle) ? state!.Title : modal.PanelTitle.Trim();
            string newDescription = string.IsNullOrWhiteSpace(modal.Description) ? state!.Description : modal.Description.Trim().Replace("\\n", "\n");

            List<string> newButtonLabels = state!.ButtonLabels;
            if (!string.IsNullOrWhiteSpace(modal.ButtonLabels))
            {
                List<string> customLabels = modal.ButtonLabels.Trim()
                    .Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToList();

                if (customLabels.Count != state!.RoleIds.Count)
                {
                    await FollowupAsync($"Button label count ({customLabels.Count}) must match role count ({state!.RoleIds.Count}). Using role names instead.", ephemeral: true);
                }
                else
                {
                    newButtonLabels = customLabels;
                }
            }

            state!.Title = newTitle;
            state.Description = newDescription;
            state.ButtonLabels = newButtonLabels;
            state.ImageUrl = uploadedImageUrl;
            logger.LogInformation("Updated role panel state for user {UserId}, showing preview", Context.User.Id);

            await ShowPreviewAsync(state, "Updated. Confirm to post.", includeSkipped: false);
        }

        [ComponentInteraction("rolepanel-cancel")]
        public async Task HandleCancelAsync()
        {
            await DeferAsync(ephemeral: true);
            stateService.Clear(Context.User.Id);

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = "Role panel creation cancelled.";
                msg.Embed = null;
                msg.Components = new ComponentBuilder().Build();
                msg.Flags = MessageFlags.Ephemeral;
            });

            logger.LogInformation("Role panel creation cancelled by {UserId}", Context.User.Id);
        }

        [ComponentInteraction("rolepanel-toggle:*")]
        public async Task HandleRoleToggleAsync(string roleIdValue)
        {
            await DeferAsync(ephemeral: true);

            if (!ulong.TryParse(roleIdValue, out ulong roleId))
            {
                await FollowupAsync("That role button is no longer valid.", ephemeral: true);
                return;
            }

            SocketGuild? guild = Context.Guild;
            if (guild == null)
            {
                await FollowupAsync("This only works inside a server.", ephemeral: true);
                return;
            }

            IRole? role = guild.GetRole(roleId);
            SocketGuildUser? user = guild.GetUser(Context.User.Id);
            SocketGuildUser botUser = guild.CurrentUser;

            if (role == null || user == null)
            {
                await FollowupAsync("That role is no longer available.", ephemeral: true);
                return;
            }

            if (role.IsManaged || !botUser.GuildPermissions.ManageRoles || botUser.Hierarchy <= role.Position)
            {
                await FollowupAsync("I can't manage that role. Please ask an admin to move my role above it.", ephemeral: true);
                return;
            }

            bool hasRole = user.Roles.Any(r => r.Id == role.Id);

            try
            {
                if (hasRole)
                {
                    await user.RemoveRoleAsync(role);
                    await FollowupAsync($"Removed {role.Mention} from you.", ephemeral: true);
                    logger.LogInformation("Removed role {RoleId} from user {UserId}", role.Id, user.Id);
                }
                else
                {
                    await user.AddRoleAsync(role);
                    await FollowupAsync($"Added {role.Mention} to you.", ephemeral: true);
                    logger.LogInformation("Added role {RoleId} to user {UserId}", role.Id, user.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to toggle role {RoleId} for user {UserId}", role.Id, user.Id);
                await FollowupAsync("Couldn't update your role. Please try again or contact a moderator.", ephemeral: true);
            }
        }

        private async Task ShowPreviewAsync(UserRolePanelFormState state, string content, bool includeSkipped)
        {
            SocketGuild guild = Context.Guild!;
            List<IRole> roles = ResolveRoles(guild, state.RoleIds);
            List<IRole> skippedRoles = includeSkipped ? ResolveRoles(guild, state.SkippedRoleIds) : new();

            string displayName = (Context.User as SocketGuildUser)?.Nickname ?? Context.User.Username;
            string avatarUrl = Context.User.GetAvatarUrl(size: 512);

            Embed embed = BuildEmbed(state, displayName, avatarUrl, "(Preview)");

            ComponentBuilder builder = new ComponentBuilder()
                .WithButton("Confirm", "rolepanel-confirm", ButtonStyle.Success, row: 0)
                .WithButton("Edit", "rolepanel-edit", ButtonStyle.Secondary, row: 0)
                .WithButton("Cancel", "rolepanel-cancel", ButtonStyle.Danger, row: 0);

            string skippedText = skippedRoles.Count > 0 ? $"\nSkipped (can't manage): {string.Join(", ", skippedRoles.Select(r => r.Mention))}" : string.Empty;

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = $"{content}{skippedText}";
                msg.Embed = embed;
                msg.Components = builder.Build();
                msg.Flags = MessageFlags.Ephemeral;
            });
        }

        private static Embed BuildEmbed(UserRolePanelFormState state, string displayName, string avatarUrl, string? titleSuffix)
        {
            EmbedBuilder embed = new EmbedBuilder()
                .WithAuthor(displayName, avatarUrl, "https://discord.gg/Cddu5aJ")
                .WithTitle(titleSuffix == null ? state.Title : $"{state.Title} {titleSuffix}")
                .WithDescription(state.Description)
                .WithColor(Color.DarkBlue)
                .WithCurrentTimestamp();

            if (!string.IsNullOrEmpty(state.ImageUrl))
                embed.WithImageUrl(state.ImageUrl);

            return embed.Build();
        }

        private static ComponentBuilder BuildRoleButtons(UserRolePanelFormState state, List<IRole> roles)
        {
            ComponentBuilder components = new ComponentBuilder();
            for (int i = 0; i < roles.Count; i++)
            {
                IRole role = roles[i];
                string buttonLabel = i < state.ButtonLabels.Count ? state.ButtonLabels[i] : role.Name;

                components.WithButton(
                    label: buttonLabel,
                    customId: $"rolepanel-toggle:{role.Id}",
                    style: ButtonStyle.Secondary,
                    row: i / 5);
            }

            return components;
        }

        private static List<IRole> ResolveRoles(SocketGuild guild, IEnumerable<ulong> roleIds) =>
            roleIds.Select(id => guild.GetRole(id)).Where(r => r != null).Cast<IRole>().OrderByDescending(r => r.Position).ToList();
    }
}