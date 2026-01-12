using System.Text.RegularExpressions;
using Discord.Interactions;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class LiamCommand : LoggedCommandModule
    {
        private const ulong LiamChannelId = 1347675225873186950; // #liams-memes

        private static readonly Regex UrlRegex = new(@"\bhttps?://\S+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        [SlashCommand("liam", "Fetches a random meme from the sacred #liams-memes channel.")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task LiamAsync()
        {
            await DeferAsync(ephemeral: true);
            await LogCommandAsync();

            try
            {
                IMessageChannel? channel = Context.Client.GetChannel(LiamChannelId) as IMessageChannel;

                if (channel == null || channel is not ITextChannel textChannel)
                {
                    await FollowupAsync("Couldn't find the sacred Liam channel. RIP memes.", ephemeral: true);
                    return;
                }

                // Fetch messages in batches (up to ~10,000 – Discord.Net caps at 100 per fetch)
                List<IMessage> allMessages = new List<IMessage>();
                ulong? lastId = (ulong?)null;
                const int batchSize = 100;

                for (int i = 0; i < 100; i++) // max 10,000 messages
                {
                    IEnumerable<IMessage> messages = lastId.HasValue
                        ? await textChannel.GetMessagesAsync(lastId.Value, Direction.Before, batchSize).FlattenAsync()
                        : await textChannel.GetMessagesAsync(batchSize).FlattenAsync();
                    List<IMessage> batch = messages.ToList();
                    if (!batch.Any()) break;

                    allMessages.AddRange(batch);
                    lastId = batch.Last().Id;

                    if (batch.Count < batchSize) break;
                }

                if (allMessages.Count == 0)
                {
                    await FollowupAsync("No messages found in #liams-memes. Has Liam forsaken us?", ephemeral: true);
                    return;
                }

                // Filter messages that likely contain images
                List<IMessage> memeCandidates = [.. allMessages
                    .Where(m =>
                        m.Attachments.Any(a => a.Width > 0 || a.Height > 0) || // actual image/video
                        m.Embeds.Any(e => e.Image.HasValue || e.Thumbnail.HasValue) ||
                        UrlRegex.IsMatch(m.Content))];

                if (!memeCandidates.Any())
                {
                    await FollowupAsync("No memes found. Liam is sleeping.", ephemeral: true);
                    return;
                }

                // Pick random meme
                IMessage randomMeme = memeCandidates[Random.Shared.Next(memeCandidates.Count)];

                // Extract best image URL
                string? imageUrl = randomMeme.Attachments
                    .FirstOrDefault(a => a.Width > 0 || a.Height > 0)?.Url
                    ?? randomMeme.Embeds.FirstOrDefault(e => e.Image.HasValue)?.Image?.Url
                    ?? randomMeme.Embeds.FirstOrDefault(e => e.Thumbnail.HasValue)?.Thumbnail?.Url
                    ?? UrlRegex.Match(randomMeme.Content).Value;

                if (string.IsNullOrEmpty(imageUrl))
                {
                    await FollowupAsync("Found a meme but couldn't extract image. Sad.", ephemeral: true);
                    return;
                }

                EmbedBuilder embed = new EmbedBuilder()
                    .WithAuthor(
                        name: randomMeme.Author.Username,
                        iconUrl: randomMeme.Author.GetAvatarUrl(ImageFormat.Auto, 128) ?? randomMeme.Author.GetDefaultAvatarUrl())
                    .WithTitle($"Random Liam Meme #{memeCandidates.IndexOf(randomMeme) + 1}")
                    .WithDescription($"[Jump to original]({randomMeme.GetJumpUrl()})")
                    .WithImageUrl(imageUrl)
                    .WithColor(new Color(0xff69b4)) // Hot pink, baby
                    .WithFooter($"From #liams-memes • {memeCandidates.Count} memes in cache")
                    .WithTimestamp(randomMeme.Timestamp);

                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Liam command failed: {ex.Message}", ephemeral: true);
                Console.WriteLine($"[LiamCommand] Error: {ex}");
            }
        }
    }
}