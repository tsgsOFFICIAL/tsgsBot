using Discord.Interactions;
using Discord.WebSocket;
using tsgsBot_C_.Models;
using Discord;

namespace tsgsBot_C_.Bot.Commands.Public
{
    public sealed class CreateTodoCommand : LoggedCommandModule
    {
        private const int MaxItemsWithoutOverflow = 8;
        private const int MaxItemsWithOverflow = 18;
        private const int OverflowButtonItemCount = 6;
        private const int MaxItemTextLength = 120;

        [SlashCommand("create-todo", "Create a todo list with buttons")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task CreateTodoAsync(
            [Summary("name", "Todo list name")] string name,
            [Summary("role", "Role allowed to manage this list")] IRole role)
        {
            string listName = string.IsNullOrWhiteSpace(name) ? "Todo List" : name.Trim();

            string displayName = (Context.User as SocketGuildUser)?.Nickname ?? Context.User.Username;
            string avatarUrl = Context.User.GetAvatarUrl(size: 512);

            EmbedBuilder embed = new EmbedBuilder()
                .WithAuthor(displayName, avatarUrl, "https://discord.gg/Cddu5aJ")
                .WithTitle($"📝 Todo: {listName}")
                .WithDescription("No items yet.")
                .WithColor(Color.DarkGrey)
                .WithCurrentTimestamp()
                .AddField("Allowed Role", role.Mention, true)
                .WithFooter(BuildFooter(role.Id, Context.User.Id));

            MessageComponent components = BuildComponents([]);

            await Context.Channel.SendMessageAsync(embed: embed.Build(), components: components);
            await RespondAsync("\u200B", ephemeral: true);
            await DeleteOriginalResponseAsync();

            await LogCommandAsync(("name", listName), ("role", role));
        }

        [ComponentInteraction("todo-add")]
        public async Task HandleAddAsync()
        {
            if (Context.Interaction is not SocketMessageComponent component)
                return;

            if (!TryGetEmbed(component.Message, out IEmbed? embed, out string error))
            {
                await RespondAsync(error, ephemeral: true);
                return;
            }

            if (!TryGetAccess(embed!, out ulong roleId, out ulong ownerId))
            {
                await RespondAsync("This todo list is missing permissions metadata.", ephemeral: true);
                return;
            }

            if (!HasEditAccess(roleId, ownerId))
            {
                await RespondAsync("You don’t have permission to edit this list.", ephemeral: true);
                return;
            }

            ModalBuilder modal = new ModalBuilder()
                .WithTitle("Add Todo Item")
                .WithCustomId($"todo-add:{component.Message.Id}:{component.Channel.Id}")
                .AddTextInput("Item", "todo_item", TextInputStyle.Short, placeholder: "e.g. Update docs", required: true, maxLength: MaxItemTextLength);

            await RespondWithModalAsync(modal.Build());
        }

        [ModalInteraction("todo-add:.*", TreatAsRegex = true)]
        public async Task HandleAddModalAsync(TodoAddItemModalModel modal)
        {
            await DeferAsync(ephemeral: true);

            if (Context.Interaction is not SocketModal socketModal)
            {
                await FollowupAsync("Unable to read modal data.", ephemeral: true);
                return;
            }

            if (!TryParseAddModalCustomId(socketModal.Data.CustomId, out ulong messageId, out ulong channelId))
            {
                await FollowupAsync("Invalid todo list reference.", ephemeral: true);
                return;
            }

            if (Context.Client.GetChannel(channelId) is not IMessageChannel channel)
            {
                await FollowupAsync("Todo list channel not found.", ephemeral: true);
                return;
            }

            if (await channel.GetMessageAsync(messageId) is not IUserMessage message)
            {
                await FollowupAsync("Todo list message not found.", ephemeral: true);
                return;
            }

            if (!TryGetEmbed(message, out IEmbed? embed, out string error))
            {
                await FollowupAsync(error, ephemeral: true);
                return;
            }

            if (!TryGetAccess(embed!, out ulong roleId, out ulong ownerId))
            {
                await FollowupAsync("This todo list is missing permissions metadata.", ephemeral: true);
                return;
            }

            if (!HasEditAccess(roleId, ownerId))
            {
                await FollowupAsync("You don’t have permission to edit this list.", ephemeral: true);
                return;
            }

            List<TodoItem> items = ParseItems(embed!);
            if (items.Count >= MaxItemsWithOverflow)
            {
                await FollowupAsync($"This list is full (max {MaxItemsWithOverflow} items).", ephemeral: true);
                return;
            }

            string itemText = modal.Item.Trim();
            if (string.IsNullOrWhiteSpace(itemText))
            {
                await FollowupAsync("Item cannot be empty.", ephemeral: true);
                return;
            }

            items.Add(new TodoItem(itemText, false));

            Embed updatedEmbed = BuildUpdatedEmbed(embed!, items);
            MessageComponent components = BuildComponents(items);

            await message.ModifyAsync(msg =>
            {
                msg.Embed = updatedEmbed;
                msg.Components = components;
            });

            await DeferAsync(ephemeral: true);
            await DeleteOriginalResponseAsync();
        }

        [ComponentInteraction("todo-clear")]
        public async Task HandleClearAsync()
        {
            if (Context.Interaction is not SocketMessageComponent component)
                return;

            if (!TryGetEmbed(component.Message, out IEmbed? embed, out string error))
            {
                await RespondAsync(error, ephemeral: true);
                return;
            }

            if (!TryGetAccess(embed!, out ulong roleId, out ulong ownerId))
            {
                await RespondAsync("This todo list is missing permissions metadata.", ephemeral: true);
                return;
            }

            if (!HasEditAccess(roleId, ownerId))
            {
                await RespondAsync("You don’t have permission to edit this list.", ephemeral: true);
                return;
            }

            List<TodoItem> items = [];
            Embed updatedEmbed = BuildUpdatedEmbed(embed!, items);

            await component.UpdateAsync(msg =>
            {
                msg.Embed = updatedEmbed;
                msg.Components = BuildComponents(items);
            });
        }

        [ComponentInteraction("todo-close")]
        public async Task HandleCloseAsync()
        {
            if (Context.Interaction is not SocketMessageComponent component)
                return;

            if (!TryGetEmbed(component.Message, out IEmbed? embed, out string error))
            {
                await RespondAsync(error, ephemeral: true);
                return;
            }

            if (!TryGetAccess(embed!, out ulong roleId, out ulong ownerId))
            {
                await RespondAsync("This todo list is missing permissions metadata.", ephemeral: true);
                return;
            }

            if (!HasEditAccess(roleId, ownerId))
            {
                await RespondAsync("You don’t have permission to close this list.", ephemeral: true);
                return;
            }

            await component.Message.DeleteAsync();
        }

        [ComponentInteraction("todo-toggle:*")]
        public async Task HandleToggleAsync(string indexValue)
        {
            if (Context.Interaction is not SocketMessageComponent component)
                return;

            if (!TryGetEmbed(component.Message, out IEmbed? embed, out string error))
            {
                await RespondAsync(error, ephemeral: true);
                return;
            }

            if (!TryGetAccess(embed!, out ulong roleId, out ulong ownerId))
            {
                await RespondAsync("This todo list is missing permissions metadata.", ephemeral: true);
                return;
            }

            if (!HasEditAccess(roleId, ownerId))
            {
                await RespondAsync("You don’t have permission to edit this list.", ephemeral: true);
                return;
            }

            if (!int.TryParse(indexValue, out int index))
            {
                await RespondAsync("Invalid item reference.", ephemeral: true);
                return;
            }

            List<TodoItem> items = ParseItems(embed!);
            if (index < 0 || index >= items.Count)
            {
                await RespondAsync("That item no longer exists.", ephemeral: true);
                return;
            }

            TodoItem item = items[index];
            items[index] = item with { IsComplete = !item.IsComplete };

            Embed updatedEmbed = BuildUpdatedEmbed(embed!, items);

            await component.UpdateAsync(msg =>
            {
                msg.Embed = updatedEmbed;
                msg.Components = BuildComponents(items);
            });
        }

        [ComponentInteraction("todo-remove:*")]
        public async Task HandleRemoveAsync(string indexValue)
        {
            if (Context.Interaction is not SocketMessageComponent component)
                return;

            if (!TryGetEmbed(component.Message, out IEmbed? embed, out string error))
            {
                await RespondAsync(error, ephemeral: true);
                return;
            }

            if (!TryGetAccess(embed!, out ulong roleId, out ulong ownerId))
            {
                await RespondAsync("This todo list is missing permissions metadata.", ephemeral: true);
                return;
            }

            if (!HasEditAccess(roleId, ownerId))
            {
                await RespondAsync("You don’t have permission to edit this list.", ephemeral: true);
                return;
            }

            if (!int.TryParse(indexValue, out int index))
            {
                await RespondAsync("Invalid item reference.", ephemeral: true);
                return;
            }

            List<TodoItem> items = ParseItems(embed!);
            if (index < 0 || index >= items.Count)
            {
                await RespondAsync("That item no longer exists.", ephemeral: true);
                return;
            }

            items.RemoveAt(index);

            Embed updatedEmbed = BuildUpdatedEmbed(embed!, items);

            await component.UpdateAsync(msg =>
            {
                msg.Embed = updatedEmbed;
                msg.Components = BuildComponents(items);
            });
        }

        [ComponentInteraction("todo-overflow")]
        public async Task HandleOverflowSelectAsync(string[] values)
        {
            if (values.Length == 0)
                return;

            if (Context.Interaction is not SocketMessageComponent component)
                return;

            if (!TryGetEmbed(component.Message, out IEmbed? embed, out string error))
            {
                await RespondAsync(error, ephemeral: true);
                return;
            }

            if (!TryGetAccess(embed!, out ulong roleId, out ulong ownerId))
            {
                await RespondAsync("This todo list is missing permissions metadata.", ephemeral: true);
                return;
            }

            if (!HasEditAccess(roleId, ownerId))
            {
                await RespondAsync("You don’t have permission to edit this list.", ephemeral: true);
                return;
            }

            string[] parts = values[0].Split(':', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out int index))
            {
                await RespondAsync("Invalid selection.", ephemeral: true);
                return;
            }

            List<TodoItem> items = ParseItems(embed!);
            if (index < 0 || index >= items.Count)
            {
                await RespondAsync("That item no longer exists.", ephemeral: true);
                return;
            }

            if (parts[0] == "toggle")
            {
                TodoItem item = items[index];
                items[index] = item with { IsComplete = !item.IsComplete };
            }
            else if (parts[0] == "remove")
            {
                items.RemoveAt(index);
            }
            else
            {
                await RespondAsync("Invalid selection.", ephemeral: true);
                return;
            }

            Embed updatedEmbed = BuildUpdatedEmbed(embed!, items);

            await component.UpdateAsync(msg =>
            {
                msg.Embed = updatedEmbed;
                msg.Components = BuildComponents(items);
            });
        }

        private static bool TryGetEmbed(IUserMessage message, out IEmbed? embed, out string error)
        {
            embed = message.Embeds.FirstOrDefault();
            if (embed == null)
            {
                error = "Todo list embed not found.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static List<TodoItem> ParseItems(IEmbed embed)
        {
            List<TodoItem> items = [];
            if (string.IsNullOrWhiteSpace(embed.Description) || embed.Description == "No items yet.")
                return items;

            string[] lines = embed.Description.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string line in lines)
            {
                // Format: "**1.** ◻️ text" or "**1.** ✅ ~~text~~"
                int dotIndex = line.IndexOf('.');
                if (dotIndex == -1)
                    continue;

                string remainder = line[(dotIndex + 1)..].Trim();
                // Remove closing bold markdown if present
                if (remainder.StartsWith("**"))
                    remainder = remainder[2..].Trim();

                if (remainder.StartsWith("◻️"))
                {
                    string text = remainder[1..].Trim(); // ◻️ is 1 char unit
                    items.Add(new TodoItem(text, false));
                }
                else if (remainder.StartsWith("✅"))
                {
                    string text = remainder[1..].Trim(); // ✅ is 1 char unit
                    // Remove strikethrough markers if present
                    if (text.StartsWith("~~") && text.EndsWith("~~"))
                        text = text[2..^2];
                    items.Add(new TodoItem(text, true));
                }
            }

            return items;
        }

        private static string BuildDescription(List<TodoItem> items)
        {
            if (items.Count == 0)
                return "No items yet.";

            return string.Join("\n", items.Select((item, i) =>
            {
                string text = item.IsComplete ? $"~~{item.Text}~~" : item.Text;
                string icon = item.IsComplete ? "✅" : "◻️";
                return $"**{i + 1}.** {icon}  {text}";
            }));
        }

        private static Embed BuildUpdatedEmbed(IEmbed original, List<TodoItem> items)
        {
            EmbedBuilder builder = original.ToEmbedBuilder();
            builder.Description = BuildDescription(items);
            return builder.Build();
        }

        private static MessageComponent BuildComponents(List<TodoItem> items)
        {
            ComponentBuilder builder = new ComponentBuilder()
                .WithButton("Add Item", "todo-add", ButtonStyle.Primary, row: 0)
                .WithButton("Clear List", "todo-clear", ButtonStyle.Danger, row: 0)
                .WithButton("Close List", "todo-close", ButtonStyle.Danger, row: 0);

            if (items.Count == 0)
                return builder.Build();

            bool hasOverflow = items.Count > MaxItemsWithoutOverflow;
            int buttonItemLimit = hasOverflow ? OverflowButtonItemCount : MaxItemsWithoutOverflow;
            int buttonItems = Math.Min(buttonItemLimit, items.Count);

            for (int i = 0; i < buttonItems; i++)
            {
                int row = 1 + (i / 2);
                ButtonStyle toggleStyle = items[i].IsComplete ? ButtonStyle.Success : ButtonStyle.Secondary;
                string icon = items[i].IsComplete ? "✅" : "◻️";
                string toggleLabel = $"{icon} {i + 1}";
                string removeLabel = $"❌ {i + 1}";

                builder.WithButton(toggleLabel, $"todo-toggle:{i}", toggleStyle, row: row);
                builder.WithButton(removeLabel, $"todo-remove:{i}", ButtonStyle.Danger, row: row);
            }

            if (items.Count > buttonItems)
            {
                SelectMenuBuilder overflowMenu = new SelectMenuBuilder()
                    .WithCustomId("todo-overflow")
                    .WithPlaceholder($"Manage items {buttonItems + 1}-{items.Count}")
                    .WithMinValues(1)
                    .WithMaxValues(1);

                for (int i = buttonItems; i < items.Count; i++)
                {
                    string preview = Truncate(items[i].Text, 60);
                    overflowMenu.AddOption($"Toggle #{i + 1}", $"toggle:{i}", description: preview);
                    overflowMenu.AddOption($"Remove #{i + 1}", $"remove:{i}", description: preview);
                }

                builder.WithSelectMenu(overflowMenu, row: 4);
            }

            return builder.Build();
        }

        private static string BuildFooter(ulong roleId, ulong ownerId) => $"role:{roleId}|owner:{ownerId}";

        private static bool TryGetAccess(IEmbed embed, out ulong roleId, out ulong ownerId)
        {
            roleId = 0;
            ownerId = 0;

            string? footer = embed.Footer?.Text;
            if (string.IsNullOrWhiteSpace(footer))
                return false;

            string[] parts = footer.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string part in parts)
            {
                if (part.StartsWith("role:") && ulong.TryParse(part[5..], out ulong parsedRole))
                    roleId = parsedRole;
                else if (part.StartsWith("owner:") && ulong.TryParse(part[6..], out ulong parsedOwner))
                    ownerId = parsedOwner;
            }

            return roleId != 0 || ownerId != 0;
        }

        private bool HasEditAccess(ulong roleId, ulong ownerId)
        {
            if (Context.User is not SocketGuildUser guildUser)
                return false;

            if (guildUser.GuildPermissions.Administrator || guildUser.GuildPermissions.ManageMessages)
                return true;

            if (ownerId != 0 && guildUser.Id == ownerId)
                return true;

            if (roleId != 0 && guildUser.Roles.Any(r => r.Id == roleId))
                return true;

            return false;
        }

        private static bool TryParseAddModalCustomId(string customId, out ulong messageId, out ulong channelId)
        {
            messageId = 0;
            channelId = 0;

            string[] parts = customId.Split(':');
            if (parts.Length != 3)
                return false;

            return ulong.TryParse(parts[1], out messageId) && ulong.TryParse(parts[2], out channelId);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (value.Length <= maxLength)
                return value;

            return value[..(maxLength - 1)] + "…";
        }

        private sealed record TodoItem(string Text, bool IsComplete);
    }
}
