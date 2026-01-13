using Discord.Interactions;
using Discord;

namespace tsgsBot_C_.Commands.Restricted
{
    public sealed class GiveawayCommand : LoggedCommandModule
    {
        [SlashCommand("giveaway", "Start a giveaway where users can participate by reacting.")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.CreateEvents)]
        public async Task GiveawayAsync(
            [Summary("prize", "The prize for the giveaway")] string prize,
            [Summary("winners", "Number of winners (default: 1)")] int winners = 1,
            [Summary("reaction_emoji", "The emoji to react with (default: 🎟️)")] string reactionEmoji = "🎟️",
            [Summary("date", "End date in YYYY-MM-DD (default: today)")] string? date = null,
            [Summary("endtime", "End time in HH:MM (24-hour, default: 1 hour from now)")] string? endTime = null)
        {
            await LogCommandAsync(("prize", prize), ("winners", winners), ("emoji", reactionEmoji), ("date", date), ("endtime", endTime));

            try
            {
                await DeferAsync(ephemeral: true);

                if (Context.Channel is not IMessageChannel channel)
                {
                    await FollowupAsync("This command must be used in a text channel.", ephemeral: true);
                    return;
                }

                // Default date to today
                date ??= DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

                // Default endTime to 1 hour from now
                if (endTime == null)
                {
                    DateTimeOffset defaultEnd = DateTimeOffset.UtcNow.AddHours(1);
                    endTime = defaultEnd.ToString("HH:mm");
                }

                // Parse date/time
                if (!DateTimeOffset.TryParse($"{date} {endTime}", out DateTimeOffset endDateTime) || endDateTime <= DateTimeOffset.UtcNow)
                {
                    await FollowupAsync("Invalid or past end date/time. Use YYYY-MM-DD and HH:mm (24-hour).", ephemeral: true);
                    return;
                }

                TimeSpan delay = endDateTime - DateTimeOffset.UtcNow;

                // Build embed
                EmbedBuilder embed = new EmbedBuilder()
                    .WithTitle("🎉 Giveaway!")
                    .WithDescription(
                        $"**Prize:** {prize}\n\n" +
                        $"React with {reactionEmoji} to enter!\n\n" +
                        $"🏆 **Winners:** {winners}\n" +
                        $"⏳ **Ends:** <t:{endDateTime.ToUnixTimeSeconds()}:R>")
                    .WithColor(new Color(0xffcc00))
                    .WithFooter("Ends at")
                    .WithTimestamp(endDateTime);

                // Send giveaway message
                IUserMessage? giveawayMsg = await channel.SendMessageAsync(embed: embed.Build());

                // Add reaction
                IEmote emote;
                if (Emote.TryParse(reactionEmoji, out Emote? parsedEmote))
                    emote = parsedEmote;
                else
                    emote = new Emoji(reactionEmoji);

                await giveawayMsg.AddReactionAsync(emote);

                await FollowupAsync("Giveaway started successfully!", ephemeral: true);

                // Wait for end and announce winners
                await Task.Delay(delay);

                // Refresh message to get reactions
                giveawayMsg = await channel.GetMessageAsync(giveawayMsg.Id) as IUserMessage;

                if (giveawayMsg != null)
                {
                    List<IUser> reactionUsers = new List<IUser>();
                    if (giveawayMsg.Reactions.TryGetValue(emote, out ReactionMetadata reactionMeta))
                    {
                        // Get users who reacted with the specified emoji
                        await foreach (IReadOnlyCollection<IUser>? users in giveawayMsg.GetReactionUsersAsync(emote, int.MaxValue))
                        {
                            reactionUsers.AddRange(users);
                        }
                    }
                    List<ulong> participants = [.. reactionUsers
                        .Where(u => !u.IsBot)
                        .Select(u => u.Id)];

                    // Pick winners (random shuffle)
                    Random random = new Random();
                    participants = participants.OrderBy(x => random.Next()).ToList();
                    List<ulong> winnersList = participants.Take(Math.Min(winners, participants.Count)).ToList();

                    string winnerMentions = string.Join(", ", winnersList.Select(id => $"<@{id}>"));

                    EmbedBuilder resultEmbed = new EmbedBuilder()
                        .WithTitle("🎉 Giveaway Ended!")
                        .WithDescription(
                            $"**Prize:** {prize}\n\n" +
                            $"🏆 **Winner{(winners > 1 ? "s" : "")}:** {winnerMentions ?? "No winners"}\n\n" +
                            $"📋 **Entr{(participants.Count > 1 ? "ies" : "y")}:** {participants.Count}")
                        .WithColor(Color.Green)
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    await channel.SendMessageAsync(embed: resultEmbed.Build());

                    // Clean up original message
                    await giveawayMsg.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }
    }
}