using Discord;
using Discord.Interactions;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class UptimeCommand : LoggedCommandModule
    {
        [SlashCommand("uptime", "Displays the bot's uptime.")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task UptimeAsync()
        {
            // 1. Log command usage (no parameters needed)
            await LogCommandAsync();

            // 2. Calculate uptime
            TimeSpan uptime = DateTimeOffset.UtcNow - SharedProperties.Instance.UpTime;

            // 3. Build human-readable string
            List<string> parts = new List<string>();

            if (uptime.Days >= 365)
            {
                int years = uptime.Days / 365;
                parts.Add($"{years} year{(years > 1 ? "s" : "")}");
            }

            int remainingDays = uptime.Days % 365;

            if (remainingDays >= 30)
            {
                int months = remainingDays / 30; // approximate, as before
                parts.Add($"{months} month{(months > 1 ? "s" : "")}");
            }

            remainingDays %= 30;

            if (remainingDays >= 7)
            {
                int weeks = remainingDays / 7;
                parts.Add($"{weeks} week{(weeks > 1 ? "s" : "")}");
            }

            remainingDays %= 7;

            if (remainingDays > 0)
                parts.Add($"{remainingDays} day{(remainingDays > 1 ? "s" : "")}");

            if (uptime.Hours > 0)
                parts.Add($"{uptime.Hours} hour{(uptime.Hours > 1 ? "s" : "")}");

            if (uptime.Minutes > 0)
                parts.Add($"{uptime.Minutes} minute{(uptime.Minutes > 1 ? "s" : "")}");

            // Always show seconds if everything else is zero, or if there are seconds
            if (uptime.Seconds > 0 || parts.Count == 0)
                parts.Add($"{uptime.Seconds} second{(uptime.Seconds > 1 ? "s" : "")}");

            string uptimeMessage = $"The bot has been up for {string.Join(", ", parts)}.";

            // 4. Send ephemeral response
            await RespondAsync(uptimeMessage, ephemeral: true);
        }
    }

    // cleaner TimeSpan extension methods
    public static class TimeSpanExtensions
    {
        public static int GetYears(this TimeSpan ts) => ts.Days / 365;
        public static int GetMonthsApproximate(this TimeSpan ts) => (ts.Days % 365) / 30;
        public static int GetWeeks(this TimeSpan ts) => ((ts.Days % 365) % 30) / 7;
        public static int GetRemainingDaysAfterWeeks(this TimeSpan ts) => ((ts.Days % 365) % 30) % 7;
    }
}