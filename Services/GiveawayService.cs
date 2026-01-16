using Discord.WebSocket;
using tsgsBot_C_.Models;
using Discord;

namespace tsgsBot_C_.Services
{
    public sealed class GiveawayService(ILogger<PollService> logger)
    {
        public async Task FinalizeGiveawayAsync(IUserMessage message, string question, List<string> answers, List<string> emojis, int pollId, ulong createdByUserId)
        {
            try
            {
                // Mark as ended in DB (idempotent)
                await DatabaseService.Instance.UpdatePollEndedAsync(pollId);

                // Optional: double-check it wasn't already ended
                DatabasePollModel? poll = await DatabaseService.Instance.GetPollAsync(pollId);
                if (poll == null)
                {
                    logger.LogInformation("Poll {PollId} not found", pollId);
                    return;
                }

                // Populate reaction users (helps with accurate counts)
                foreach (KeyValuePair<IEmote, ReactionMetadata> reaction in message.Reactions)
                {
                    await message.GetReactionUsersAsync(reaction.Key, 1000).FlattenAsync();
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
                IUser createdByUser = await message.Channel.GetUserAsync(createdByUserId);
                string displayName = (createdByUser as SocketGuildUser)?.Nickname ?? "Unknown";
                string avatarUrl = createdByUser.GetAvatarUrl(size: 512);

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

                logger.LogInformation("Successfully finalized poll {PollId}", pollId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to finalize poll {PollId}", pollId);
            }
        }
    }
}