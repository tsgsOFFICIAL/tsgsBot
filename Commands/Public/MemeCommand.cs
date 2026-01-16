using Discord.Interactions;
using System.Text.Json;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class MemeCommand : LoggedCommandModule
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        [SlashCommand("meme", "Fetches a random meme from Reddit")]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task MemeAsync(
            [Summary("subreddit", "Subreddit to fetch from (default: comedyheaven)")]
            string subreddit = "comedyheaven")
        {
            await LogCommandAsync(("subreddit", subreddit));

            try
            {
                // Clean subreddit input (remove r/ prefix if present)
                subreddit = subreddit.Trim().Replace("r/", "", StringComparison.OrdinalIgnoreCase);

                string apiUrl = $"https://meme-api.com/gimme/{subreddit}";

                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);

                // MemeAPI response usually has "url" field with direct image link
                if (doc.RootElement.TryGetProperty("url", out JsonElement urlProp) &&
                    urlProp.ValueKind == JsonValueKind.String)
                {
                    string memeUrl = urlProp.GetString()!;

                    // Optional: check if it's actually an image link (basic validation)
                    if (memeUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        memeUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        memeUrl.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                    {
                        await RespondAsync(memeUrl, ephemeral: true);
                    }
                    else
                    {
                        // Sometimes meme-api returns video or gallery links
                        await RespondAsync($"Got a link, but it might not be a direct image:\n{memeUrl}\n(ephemeral)", ephemeral: true);
                    }
                }
                else
                {
                    await RespondAsync("Couldn't find a meme URL in the response. Try a different subreddit?", ephemeral: true);
                }
            }
            catch (HttpRequestException ex)
            {
                await RespondAsync($"Failed to fetch meme: {ex.Message}\n(subreddit might not exist or API is down)", ephemeral: true);
            }
            catch
            {
                await RespondAsync("Something went wrong while fetching your meme. Try again later.", ephemeral: true);
            }
        }
    }
}