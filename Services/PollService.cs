using Discord.WebSocket;
using tsgsBot_C_.Models;
using Discord;

namespace tsgsBot_C_.Services
{
    /// <summary>
    /// Provides functionality to finalize polls by counting votes, generating result summaries, updating poll status in
    /// the database, and posting final results to the channel.
    /// </summary>
    /// <remarks>This service is intended for use in scenarios where interactive polls are managed and results
    /// need to be published after completion. It ensures that poll data is accurately finalized and that results are
    /// communicated to users. The class is sealed and should not be inherited.</remarks>
    /// <param name="logger">The logger used to record informational and error messages related to poll finalization.</param>
    public sealed class PollService(ILogger<PollService> logger)
    {
        /// <summary>
        /// Finalizes a poll by calculating vote results, deleting the original poll message, and posting the final
        /// results to the channel.
        /// </summary>
        /// <remarks>If the poll has already ended or cannot be found, no results are posted and the
        /// operation completes silently. The method subtracts the bot's own reaction from each vote count to ensure
        /// accurate results. The results embed highlights the winning option or indicates a tie if
        /// applicable.</remarks>
        /// <param name="message">The user message representing the poll to be finalized. Must be a valid poll message in the channel.</param>
        /// <param name="question">The poll question to display in the final results embed.</param>
        /// <param name="answers">A list of answer options corresponding to the poll choices. Each answer should match the order of the
        /// provided emojis.</param>
        /// <param name="emojis">A list of emojis used as reaction options for the poll. Each emoji should correspond to an answer in the
        /// same order.</param>
        /// <param name="pollId">The unique identifier of the poll to finalize. Used to update poll status and retrieve poll data.</param>
        /// <returns>A task that represents the asynchronous operation of finalizing the poll. The task completes when the poll
        /// results have been posted and the original message deleted.</returns>
        public async Task FinalizePollAsync(IUserMessage message, string question, List<string> answers, List<string> emojis, int pollId, ulong createdByUserId)
        {
            try
            {
                DatabasePollModel? poll = await DatabaseService.Instance.GetPollAsync(pollId);
                if (poll == null)
                {
                    logger.LogInformation("Poll {PollId} not found", pollId);
                    return;
                }

                if (poll.HasEnded)
                {
                    logger.LogInformation("Poll {PollId} already finalized", pollId);
                    return;
                }

                // Populate reaction users (helps with accurate counts)
                foreach (KeyValuePair<IEmote, ReactionMetadata> reaction in message.Reactions)
                {
                    await message.GetReactionUsersAsync(reaction.Key, int.MaxValue).FlattenAsync();
                }

                // Count votes per option
                List<(string Emoji, string Answer, int Count)> voteCounts = new List<(string Emoji, string Answer, int Count)>();

                for (int i = 0; i < emojis.Count; i++)
                {
                    string emojiStr = emojis[i];
                    IEmote emote = Emote.TryParse(emojiStr, out Emote? parsed) ? parsed : new Emoji(emojiStr);

                    if (message.Reactions.TryGetValue(emote, out ReactionMetadata reaction))
                    {
                        int count = reaction.ReactionCount;
                        if (count > 0) count--; // subtract bot's own reaction
                        voteCounts.Add((emojiStr, answers[i], count));
                    }
                    else
                    {
                        voteCounts.Add((emojiStr, answers[i], 0));
                    }
                }

                int totalVotes = voteCounts.Sum(x => x.Count);
                List<(string Emoji, string Answer, int Count)> sorted = voteCounts.OrderByDescending(x => x.Count).ToList();

                // Build result lines
                List<string> lines = new List<string>();
                for (int idx = 0; idx < sorted.Count; idx++)
                {
                    (string Emoji, string Answer, int Count) item = sorted[idx];
                    double pct = totalVotes > 0 ? (item.Count / (double)totalVotes) * 100 : 0;
                    string bar = new string('▰', (int)Math.Round(pct / 8.33)) +
                                 new string('▱', 12 - (int)Math.Round(pct / 8.33));

                    string line = $"{item.Emoji} **{item.Answer}**\n" +
                                  $"     ┗ {item.Count,3} votes ({pct:0.0}%) {bar}";

                    if (idx == 0 && item.Count > 0)
                    {
                        if (sorted.Count > 1 && sorted[1].Count == item.Count)
                            line += " ← TIE 🤝";
                        else
                            line += " ← WINNER 👑";
                    }

                    lines.Add(line);
                }

                // Get the display name and avatar URL safely
                IUser? createdByUser = await message.Channel.GetUserAsync(createdByUserId);
                string displayName = (createdByUser as SocketGuildUser)?.Nickname ?? createdByUser?.Username ?? "Unknown";
                string avatarUrl = createdByUser?.GetAvatarUrl(size: 512) ?? string.Empty;

                // Results embed
                Embed embed = new EmbedBuilder()
                    .WithTitle("Poll Ended – Final Results")
                    .WithAuthor(displayName, avatarUrl)
                    .WithDescription($"**{question}**\n\n{string.Join("\n", lines)}\n\n**Total votes:** {totalVotes}")
                    .WithColor(totalVotes > 0 ? new Color(0x00FF00) : new Color(0x992D22))
                    .WithCurrentTimestamp()
                    .Build();

                // Clean up original poll message and post results
                await message.DeleteAsync();
                await message.Channel.SendMessageAsync(embed: embed);

                // Mark as ended in DB (idempotent)
                await DatabaseService.Instance.UpdatePollEndedAsync(pollId);

                logger.LogInformation("Successfully finalized poll {PollId}", pollId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to finalize poll {PollId}", pollId);
            }
        }
    }
}