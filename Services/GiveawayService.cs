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

                // Mark as ended in DB (idempotent)
                await DatabaseService.Instance.UpdateGiveawayEndedAsync(giveawayId);

                // Optional: double-check it wasn't already ended
                DatabaseGiveawayModel? giveaway = await DatabaseService.Instance.GetGiveawayAsync(giveawayId);
                if (giveaway == null)
                {
                    logger.LogInformation("Giveaway {GiveawayId} not found", giveawayId);
                    return;
                }

                // Populate reaction users (helps with accurate counts)
                foreach (KeyValuePair<IEmote, ReactionMetadata> reaction in message.Reactions)
                {
                    await message.GetReactionUsersAsync(reaction.Key, int.MaxValue).FlattenAsync();
                }

                // Get a list of users who reacted with the giveaway emoji
                List<IUser> reactedUsers = new List<IUser>();

                IEmote giveawayEmote = Emote.TryParse(reactionEmoji, out Emote? parsed) ? parsed : new Emoji(reactionEmoji);

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

                // Pick winners (random shuffle)
                Random random = new Random();
                participants = [.. participants.OrderBy(x => random.Next())];
                List<ulong> winnersList = [.. participants.Take(Math.Min(winnerCount, participants.Count))];

                string winnerMentions = string.Join(", ", winnersList.Select(id => $"<@{id}>"));

                // Get the display name and avatar URL safely
                IUser createdByUser = await message.Channel.GetUserAsync(createdByUserId);
                string displayName = (createdByUser as SocketGuildUser)?.Nickname ?? "Unknown";
                string avatarUrl = createdByUser.GetAvatarUrl(size: 512);

                Embed resultEmbed = new EmbedBuilder()
                    .WithTitle("🎉 Giveaway Ended!")
                    .WithAuthor(displayName, avatarUrl)
                    .WithDescription(
                        $"**Prize:** {prize}\n\n" +
                        $"🏆 **Winner{(winnerCount > 1 ? "s" : "")}:** {winnerMentions ?? "No winners"}\n\n" +
                        $"📋 **Entr{(participants.Count > 1 ? "ies" : "y")}:** {participants.Count}")
                    .WithColor(Color.Green)
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                // Mark as ended in DB (idempotent)
                await DatabaseService.Instance.UpdateGiveawayEndedAsync(giveawayId, winnersList);

                // Clean up original giveaway message and post results
                await message.DeleteAsync();
                await message.Channel.SendMessageAsync(embed: resultEmbed);

                logger.LogInformation("Successfully finalized giveaway {GiveawayId}", giveawayId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to finalize giveaway {GiveawayId}", giveawayId);
            }
        }
    }
}