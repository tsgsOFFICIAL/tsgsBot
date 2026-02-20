using Discord.WebSocket;
using tsgsBot_C_.Models;
using Discord;

namespace tsgsBot_C_.Services
{
    public sealed class GiveawayService(ILogger<GiveawayService> logger)
    {
        public async Task FinalizeGiveawayAsync(IUserMessage message, string prize, string reactionEmoji, string winners, int giveawayId, ulong createdByUserId)
        {
            try
            {
                int winnerCount = int.Parse(winners);

                logger.LogDebug("FinalizeGiveaway start GiveawayId={GiveawayId}, MessageId={MessageId}, ConfiguredEmoji='{ReactionEmoji}', WinnerCount={WinnerCount}",
                    giveawayId, message.Id, reactionEmoji, winnerCount);

                DatabaseGiveawayModel? giveaway = await DatabaseService.Instance.GetGiveawayAsync(giveawayId);
                if (giveaway == null)
                {
                    logger.LogInformation("Giveaway {GiveawayId} not found", giveawayId);
                    return;
                }

                if (giveaway.HasEnded)
                {
                    logger.LogInformation("Giveaway {GiveawayId} already finalized", giveawayId);
                    return;
                }

                // Populate reaction users (helps with accurate counts)
                foreach (KeyValuePair<IEmote, ReactionMetadata> reaction in message.Reactions)
                {
                    await message.GetReactionUsersAsync(reaction.Key, int.MaxValue).FlattenAsync();
                }

                // Get a list of users who reacted with the giveaway emoji
                List<IUser> reactedUsers = new List<IUser>();

                logger.LogDebug("Giveaway {GiveawayId} message has {ReactionCount} reaction buckets: {Reactions}",
                    giveawayId,
                    message.Reactions.Count,
                    string.Join(", ", message.Reactions.Keys.Select(k => k.ToString())));

                IEmote? giveawayEmote = message.Reactions.Keys.FirstOrDefault(e =>
                {
                    if (e is Emote emote)
                        return emote.Name == reactionEmoji.Trim(':');
                    if (e is Emoji emoji)
                        return emoji.Name == reactionEmoji;
                    return false;
                });

                if (giveawayEmote == null)
                {
                    logger.LogWarning("Giveaway emote not found on message for GiveawayId={GiveawayId}. ConfiguredEmoji='{ReactionEmoji}', AvailableReactions={Reactions}",
                        giveawayId,
                        reactionEmoji,
                        string.Join(", ", message.Reactions.Keys.Select(k => k.ToString())));
                    return;
                }

                logger.LogDebug("Matched giveaway emote for GiveawayId={GiveawayId}: {MatchedEmote}", giveawayId, giveawayEmote.ToString());

                if (message.Reactions.TryGetValue(giveawayEmote, out ReactionMetadata reactionMetadata))
                {
                    IAsyncEnumerable<IReadOnlyCollection<IUser>> reactionUsers = message.GetReactionUsersAsync(giveawayEmote, int.MaxValue);

                    await foreach (IReadOnlyCollection<IUser> users in reactionUsers)
                    {
                        reactedUsers.AddRange(users);
                    }
                }

                List<ulong> participants = [.. reactedUsers
                        .Where(u => !u.IsBot)
                        .Select(u => u.Id)];

                logger.LogDebug("Giveaway {GiveawayId} participants after bot-filter: {ParticipantCount}", giveawayId, participants.Count);

                // Pick winners (random shuffle)
                Random random = new Random();
                participants = [.. participants.OrderBy(x => random.Next())];
                List<ulong> winnersList = [.. participants.Take(Math.Min(winnerCount, participants.Count))];

                logger.LogDebug("Giveaway {GiveawayId} selected winner IDs: {WinnerIds}", giveawayId, string.Join(",", winnersList));

                string winnerMentions = string.Join(", ", winnersList.Select(id => $"<@{id}>"));

                // Get the display name and avatar URL safely
                IUser? createdByUser = await message.Channel.GetUserAsync(createdByUserId);
                string displayName = (createdByUser as SocketGuildUser)?.Nickname ?? createdByUser?.Username ?? "Unknown";
                string avatarUrl = createdByUser?.GetAvatarUrl(size: 512) ?? string.Empty;

                Embed resultEmbed = new EmbedBuilder()
                    .WithTitle("🎉 Giveaway Ended!")
                    .WithAuthor(displayName, avatarUrl)
                    .WithDescription(
                        $"**Prize:** {prize}\n\n" +
                        $"🏆 **Winner{(winnerCount > 1 ? "s" : "")}:** {winnerMentions ?? "No winners"}\n\n" +
                        $"📋 **Entr{(participants.Count > 1 ? "ies" : "y")}:** {participants.Count}\n\n" +
                        $"<@&1463842446231343261>")
                    .WithColor(Color.Green)
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                // Clean up original giveaway message and post results
                await message.DeleteAsync();
                await message.Channel.SendMessageAsync(embed: resultEmbed);

                // Mark as ended in DB (idempotent)
                await DatabaseService.Instance.UpdateGiveawayEndedAsync(giveawayId, winnersList);

                logger.LogInformation("Successfully finalized giveaway {GiveawayId}", giveawayId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to finalize giveaway {GiveawayId}", giveawayId);
            }
        }
    }
}