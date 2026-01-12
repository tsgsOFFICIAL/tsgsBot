using System.Text.RegularExpressions;
using Discord.Interactions;
using System.Text.Json;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class CsStatsCommand : LoggedCommandModule
    {
        private static readonly HttpClient _http = new HttpClient();

        private static readonly Dictionary<string, string> StatLabels = new()
        {
            { "kd_ratio",           "K/D Ratio" },
            { "adr",                "ADR (Avg Damage/Round)" },
            { "hs_percentage",      "Headshot %" },
            { "total_matches_won",  "Total Wins" },
            { "total_time_played",  "Total Time Played" },
            { "total_mvps",         "Total MVP Awards" },
            { "cs2_hours",          "CS2 Hours" },
            { "afk_percentage",     "AFK %" }
        };

        private static readonly string[] StatOrder =
        [
            "kd_ratio", "adr", "hs_percentage", "total_matches_won",
            "total_time_played", "total_mvps", "cs2_hours", "afk_percentage"
        ];

        [SlashCommand("csstats", "Fetch CS:GO/CS2 stats for a player")]
        [CommandContextType(InteractionContextType.Guild | InteractionContextType.BotDm)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task CsStatsAsync(
            [Summary("identifier", "Steam ID64, profile URL, or vanity name (e.g. tsgs)")]
            string identifier)
        {
            await DeferAsync(ephemeral: true);
            await LogCommandAsync(("identifier", identifier));

            try
            {
                // 1. Resolve to SteamID64
                string? steamId = await ResolveSteamIdAsync(identifier);
                if (string.IsNullOrEmpty(steamId))
                {
                    await FollowupAsync("Could not resolve that identifier to a valid Steam ID.", ephemeral: true);
                    return;
                }

                // 2. Get player summary (name, avatar)
                PlayerSummary? summary = await GetPlayerSummaryAsync(steamId);
                if (summary == null)
                {
                    await FollowupAsync("No player found with that Steam ID.", ephemeral: true);
                    return;
                }

                string playerName = summary.PersonaName;
                string avatarUrl = summary.AvatarMedium;

                // 3. Get CS2 stats (appid 730)
                List<StatEntry>? stats = await GetCsStatsAsync(steamId);
                if (stats == null || !stats.Any())
                {
                    await FollowupAsync("No CS2 stats available. Make sure your profile & game stats are public!", ephemeral: true);
                    return;
                }

                // 4. Get total CS2 hours from SteamWebAPI
                double cs2Hours = await GetCs2HoursFromWebApiAsync(steamId);

                // 5. Extract & calculate stats
                Dictionary<string, long> statDict = stats.ToDictionary(s => s.Name, s => s.Value);

                long headshots = GetStat(statDict, "total_kills_headshot", 0);
                long kills = GetStat(statDict, "total_kills", 0);
                long deaths = GetStat(statDict, "total_deaths", 1);
                long damage = GetStat(statDict, "total_damage_done", 0);
                long rounds = GetStat(statDict, "total_rounds_played", 1);
                long timePlayedS = GetStat(statDict, "total_time_played", 0);

                double kd = Math.Round((double)kills / deaths, 2);
                int adr = (int)Math.Round((double)damage / rounds);
                double hsPct = kills == 0 ? 0 : Math.Round((double)headshots / kills * 100, 2);
                double timeHrs = timePlayedS / 3600.0;
                double wastedH = Math.Max(cs2Hours - timeHrs, 0);
                double afkPct = cs2Hours == 0 ? 0 : Math.Round(wastedH / cs2Hours * 100, 2);

                // Format time played nicely
                int tpHours = (int)(timePlayedS / 3600);
                int tpMinutes = (int)((timePlayedS % 3600) / 60);

                // 6. Build embed fields in desired order
                List<EmbedFieldBuilder> fields = new List<EmbedFieldBuilder>();

                foreach (string key in StatOrder)
                {
                    string value = key switch
                    {
                        "kd_ratio" => kd.ToString("F2"),
                        "adr" => adr.ToString(),
                        "hs_percentage" => $"{hsPct:F2}%",
                        "cs2_hours" => $"{cs2Hours:F1} hours",
                        "afk_percentage" => $"{afkPct:F2}%",
                        "total_time_played" => $"{tpHours} h {tpMinutes} min",
                        _ => statDict.TryGetValue(key, out long v) ? v.ToString() : "—"
                    };

                    fields.Add(new EmbedFieldBuilder
                    {
                        Name = StatLabels[key],
                        Value = value,
                        IsInline = true
                    });
                }

                // 7. Final embed
                EmbedBuilder embed = new EmbedBuilder()
                    .WithAuthor(playerName, avatarUrl)
                    .WithTitle("CS2 Stats")
                    .WithDescription(
                        $"[View on Steam](https://steamcommunity.com/profiles/{steamId})\n" +
                        "*Lifetime stats (not just Premier/Competitive)*\n" +
                        "*AFK % is estimated (total hours - in-game time)*")
                    .WithColor(new Color(26, 133, 255)) // #1a85ff
                    .WithFields(fields)
                    .WithThumbnailUrl(avatarUrl)
                    .WithCurrentTimestamp()
                    .WithFooter("Powered by Steam API & Energy Drinks • Stats must be public");

                await FollowupAsync(embed: embed.Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
            }
        }

        // ────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────

        private static async Task<string?> ResolveSteamIdAsync(string input)
        {
            if (Regex.IsMatch(input, @"^\d{17}$"))
                return input;

            string? vanity = null;

            if (input.StartsWith("https://steamcommunity.com/"))
            {
                string[] parts = input.TrimEnd('/').Split('/');
                if (parts.Contains("profiles") && parts.Length > parts.ToList().IndexOf("profiles") + 1)
                {
                    string id = parts[parts.ToList().IndexOf("profiles") + 1];
                    return Regex.IsMatch(id, @"^\d{17}$") ? id : null;
                }
                if (parts.Contains("id") && parts.Length > parts.ToList().IndexOf("id") + 1)
                    vanity = parts[parts.ToList().IndexOf("id") + 1];
            }
            else
            {
                vanity = input;
            }

            if (string.IsNullOrEmpty(vanity))
                return null;

            string url = $"https://api.steampowered.com/ISteamUser/ResolveVanityURL/v1/?key={SharedProperties.Instance.STEAM_API_KEY}&vanityurl={Uri.EscapeDataString(vanity)}";
            string resp = await _http.GetStringAsync(url);
            using JsonDocument doc = JsonDocument.Parse(resp);

            if (doc.RootElement.GetProperty("response").GetProperty("success").GetInt32() == 1)
                return doc.RootElement.GetProperty("response").GetProperty("steamid").GetString();

            return null;
        }

        private static async Task<PlayerSummary?> GetPlayerSummaryAsync(string steamId)
        {
            string url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={SharedProperties.Instance.STEAM_API_KEY}&steamids={steamId}";
            string resp = await _http.GetStringAsync(url);
            using JsonDocument doc = JsonDocument.Parse(resp);

            JsonElement players = doc.RootElement.GetProperty("response").GetProperty("players");
            if (players.GetArrayLength() == 0)
                return null;

            JsonElement p = players[0];
            return new PlayerSummary(
                p.GetProperty("personaname").GetString()!,
                p.GetProperty("avatarmedium").GetString()!
            );
        }

        private static async Task<List<StatEntry>?> GetCsStatsAsync(string steamId)
        {
            string url = $"https://api.steampowered.com/ISteamUserStats/GetUserStatsForGame/v2/?appid=730&key={SharedProperties.Instance.STEAM_API_KEY}&steamid={steamId}";
            string resp = await _http.GetStringAsync(url);
            using JsonDocument doc = JsonDocument.Parse(resp);

            if (!doc.RootElement.TryGetProperty("playerstats", out JsonElement ps) ||
                !ps.TryGetProperty("stats", out JsonElement statsArr) ||
                statsArr.ValueKind != JsonValueKind.Array)
                return null;

            List<StatEntry> list = new List<StatEntry>();
            foreach (JsonElement s in statsArr.EnumerateArray())
            {
                list.Add(new StatEntry(
                    s.GetProperty("name").GetString()!,
                    s.GetProperty("value").GetInt64()
                ));
            }
            return list;
        }

        private static async Task<double> GetCs2HoursFromWebApiAsync(string steamId)
        {
            string url = $"https://www.steamwebapi.com/steam/api/profile?key={SharedProperties.Instance.STEAM_WEB_API_KEY}&state=detailed&production=1&steam_id={steamId}";
            try
            {
                string resp = await _http.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(resp);

                if (doc.RootElement.TryGetProperty("mostplayedgames", out JsonElement mpg) &&
                    mpg.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement game in mpg.EnumerateArray())
                    {
                        if (game.TryGetProperty("appid", out JsonElement app) &&
                            app.GetInt32() == 730 &&
                            game.TryGetProperty("hoursonrecord", out JsonElement hrs))
                        {
                            return hrs.GetDouble();
                        }
                    }
                }
            }
            catch { /* silent fail */ }

            return 0;
        }

        private static long GetStat(Dictionary<string, long> dict, string key, long defaultValue = 0)
            => dict.TryGetValue(key, out long v) ? v : defaultValue;

        private record PlayerSummary(string PersonaName, string AvatarMedium);
        private record StatEntry(string Name, long Value);
    }
}