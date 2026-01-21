using System.Collections.Concurrent;
using Discord.Interactions;
using Discord.WebSocket;
using Discord.Rest;
using System.Data;
using Discord;

namespace tsgsBot_C_.Bot.Commands.Moderation
{
    public class PurgeState
    {
        public bool IsNuke { get; set; }
        public List<IMessage>? Messages { get; set; } // Null for nuke
        public string? OriginalChannelName { get; set; } // For nuke rename
    }

    public sealed class PurgeCommand : LoggedCommandModule
    {
        private static readonly ConcurrentDictionary<ulong, PurgeState> _pendingPurges = new();

        private const int MaxFetchPerPage = 100;
        private const int MaxBulkDelete = 100;
        private const double BulkAgeDays = 14;

        [SlashCommand("purge", "Deletes messages or nukes the channel (if no params)")]
        [DefaultMemberPermissions(GuildPermission.ManageChannels)]
        public async Task PurgeAsync(int? amount = null, IUser? user = null, bool allChannels = false)
        {
            await DeferAsync(ephemeral: true);
            await LogCommandAsync(("amount", amount), ("user", user), ("allChannels", allChannels));

            bool isNuke = amount == null && user == null;

            if (amount.HasValue && amount.Value <= 0)
            {
                await FollowupAsync("Amount must be positive if provided.", ephemeral: true);
                return;
            }

            if (allChannels && user == null)
            {
                await FollowupAsync("The allChannels option can only be used when a user is specified.", ephemeral: true);
                return;
            }

            if (isNuke)
            {
                if (Context.User is not IGuildUser guildUser || !guildUser.GuildPermissions.ManageChannels)
                {
                    await RespondAsync("You need Manage Channels permission to nuke the channel.", ephemeral: true);
                    return;
                }
                if (!Context.Guild.CurrentUser.GuildPermissions.ManageChannels)
                {
                    await RespondAsync("Bot needs Manage Channels permission to nuke the channel.", ephemeral: true);
                    return;
                }
            }

            PurgeState state = new PurgeState
            {
                IsNuke = isNuke,
                OriginalChannelName = Context.Channel.Name
            };

            if (!isNuke)
            {
                List<IMessage> allMessages = new List<IMessage>();
                int remaining = amount ?? int.MaxValue;

                if (allChannels && user != null)
                {
                    // Fetch from all channels in the guild
                    IReadOnlyCollection<SocketTextChannel> textChannels = Context.Guild.TextChannels;

                    foreach (SocketTextChannel channel in textChannels)
                    {
                        if (remaining <= 0)
                            break;

                        // Check if bot has permissions to read message history and manage messages
                        ChannelPermissions perms = Context.Guild.CurrentUser.GetPermissions(channel);
                        if (!perms.ReadMessageHistory || !perms.ManageMessages)
                            continue;

                        ulong? before = null;

                        while (remaining > 0)
                        {
                            int fetchCount = Math.Min(remaining, MaxFetchPerPage);
                            IAsyncEnumerable<IReadOnlyCollection<IMessage>> pages;
                            if (before.HasValue)
                            {
                                pages = channel.GetMessagesAsync(before.Value, Direction.Before, fetchCount);
                            }
                            else
                            {
                                pages = channel.GetMessagesAsync(fetchCount);
                            }

                            List<IMessage> fetched = new List<IMessage>();
                            await foreach (IReadOnlyCollection<IMessage> page in pages)
                            {
                                fetched.AddRange(page);
                            }

                            if (fetched.Count == 0) break;

                            List<IMessage> filtered = fetched.Where(m => m.Author.Id == user.Id).ToList();

                            allMessages.AddRange(filtered.Take(remaining));
                            remaining -= filtered.Count;
                            before = fetched.LastOrDefault()?.Id;

                            if (filtered.Count < fetchCount) break;
                        }
                    }
                }
                else
                {
                    // Fetch from current channel only
                    if (Context.Channel is not ITextChannel channel)
                    {
                        await FollowupAsync("Invalid channel.", ephemeral: true);
                        return;
                    }

                    ulong? before = null;

                    while (remaining > 0)
                    {
                        int fetchCount = Math.Min(remaining, MaxFetchPerPage);
                        IAsyncEnumerable<IReadOnlyCollection<IMessage>> pages;
                        if (before.HasValue)
                        {
                            pages = channel.GetMessagesAsync(before.Value, Direction.Before, fetchCount);
                        }
                        else
                        {
                            pages = channel.GetMessagesAsync(fetchCount);
                        }

                        List<IMessage> fetched = new List<IMessage>();
                        await foreach (IReadOnlyCollection<IMessage> page in pages)
                        {
                            fetched.AddRange(page);
                        }

                        if (fetched.Count == 0) break;

                        List<IMessage> filtered = user != null ? fetched.Where(m => m.Author.Id == user.Id).ToList() : fetched.ToList();

                        allMessages.AddRange(filtered.Take(remaining));
                        remaining -= filtered.Count;
                        before = fetched.LastOrDefault()?.Id;

                        if (filtered.Count < fetchCount) break;
                    }
                }

                if (allMessages.Count == 0)
                {
                    await FollowupAsync("No messages match the criteria.", ephemeral: true);
                    return;
                }

                state.Messages = allMessages;
            }

            // Build confirmation
            string description = isNuke
                ? "Nuke the entire channel? (This clones it, deletes the original, and renames the clone—losing all history!)"
                : user == null
                    ? $"Delete the last {state.Messages?.Count ?? 0} messages?"
                    : $"Delete {state.Messages?.Count ?? 0} messages from {user?.Mention}?";

            EmbedBuilder embedBuilder = new EmbedBuilder()
                .WithDescription(description)
                .WithColor(Color.Orange);

            Embed embed = embedBuilder.Build();

            ComponentBuilder componentBuilder = new ComponentBuilder()
                .WithButton("Confirm", $"purge-confirm:{Context.Interaction.Id}", ButtonStyle.Success)
                .WithButton("Cancel", $"purge-cancel:{Context.Interaction.Id}", ButtonStyle.Danger);

            MessageComponent components = componentBuilder.Build();

            await FollowupAsync(embed: embed, components: components, ephemeral: true);

            _pendingPurges[Context.Interaction.Id] = state;
        }

        [ComponentInteraction("purge-confirm:*")]
        public async Task HandleConfirmAsync(string idStr)
        {
            if (!ulong.TryParse(idStr, out ulong originalInteractionId))
            {
                await RespondAsync("Invalid session.", ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true);

            if (!_pendingPurges.TryRemove(originalInteractionId, out PurgeState? state) || state == null)
            {
                await FollowupAsync("Session expired or invalid.", ephemeral: true);
                return;
            }

            ITextChannel? channel = Context.Channel as ITextChannel;
            if (channel == null)
            {
                await FollowupAsync("Invalid channel.", ephemeral: true);
                return;
            }

            if (state.IsNuke)
            {
                // Manual clone: Create new channel with copied props
                SocketGuild guild = Context.Guild;
                IReadOnlyCollection<Overwrite> originalOverwrites = channel.PermissionOverwrites;

                TextChannelProperties props = new TextChannelProperties
                {
                    Topic = channel.Topic,
                    Position = channel.Position,
                    IsNsfw = channel.IsNsfw,
                    SlowModeInterval = channel.SlowModeInterval,
                    CategoryId = channel.CategoryId
                };

                RestTextChannel restChannel = await guild.CreateTextChannelAsync(
                    (state.OriginalChannelName ?? "temp") + "-temp", // Temp name to avoid conflict
                    p =>
                    {
                        p.Topic = props.Topic;
                        p.Position = props.Position;
                        p.IsNsfw = props.IsNsfw;
                        p.SlowModeInterval = props.SlowModeInterval;
                        p.CategoryId = props.CategoryId;
                    });

                // Get the SocketTextChannel from the guild's channel cache
                SocketTextChannel? newChannel = guild.GetTextChannel(restChannel.Id);
                if (newChannel == null)
                {
                    await FollowupAsync("Failed to create new channel.", ephemeral: true);
                    return;
                }

                // Copy overwrites
                foreach (Overwrite ow in originalOverwrites)
                {
                    OverwritePermissions permissions = ow.Permissions;
                    if (ow.TargetType == PermissionTarget.Role)
                    {
                        IRole? role = guild.GetRole(ow.TargetId);
                        if (role != null)
                            await newChannel.AddPermissionOverwriteAsync(role, permissions);
                    }
                    else if (ow.TargetType == PermissionTarget.User)
                    {
                        IGuildUser? guser = guild.GetUser(ow.TargetId);
                        if (guser != null)
                            await newChannel.AddPermissionOverwriteAsync(guser, permissions);
                    }
                }

                // Delete original
                await channel.DeleteAsync();

                // Rename new to original name (now free)
                await newChannel.ModifyAsync(p => p.Name = state.OriginalChannelName);

                // Since channel is deleted, use followup instead of update
                await FollowupAsync("Channel nuked and recreated.", ephemeral: true);
            }
            else
            {
                // Purge messages
                DateTimeOffset now = DateTimeOffset.UtcNow;
                List<IMessage> bulkEligible = (state.Messages ?? new List<IMessage>()).Where(m => (now - m.CreatedAt).TotalDays < BulkAgeDays).ToList();
                List<IMessage> older = (state.Messages ?? new List<IMessage>()).Except(bulkEligible).ToList();

                for (int i = 0; i < bulkEligible.Count; i += MaxBulkDelete)
                {
                    List<IMessage> chunk = bulkEligible.Skip(i).Take(MaxBulkDelete).ToList();
                    if (chunk.Count >= 2)
                        await channel.DeleteMessagesAsync(chunk);
                    else if (chunk.Count == 1)
                        await chunk[0].DeleteAsync();

                    if (bulkEligible.Count > MaxBulkDelete) await Task.Delay(500);
                }

                foreach (IMessage msg in older)
                {
                    await msg.DeleteAsync();
                    await Task.Delay(250);
                }

                // Update the message (edits the confirmation message)
                await Context.Interaction.ModifyOriginalResponseAsync(props =>
                {
                    props.Content = $"Deleted {state.Messages?.Count ?? 0} messages.";
                    props.Components = null;
                });
            }
        }

        [ComponentInteraction("purge-cancel:*")]
        public async Task HandleCancelAsync(string idStr)
        {
            if (!ulong.TryParse(idStr, out ulong originalInteractionId))
            {
                await RespondAsync("Invalid session.", ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true);

            if (!_pendingPurges.TryRemove(originalInteractionId, out PurgeState? state) || state == null)
            {
                await FollowupAsync("Session expired or invalid.", ephemeral: true);
                return;
            }

            // Update the message (edits the confirmation message)
            await Context.Interaction.ModifyOriginalResponseAsync(props =>
            {
                props.Content = "Purge canceled.";
                props.Components = null;
            });
        }
    }
}