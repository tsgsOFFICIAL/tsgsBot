using Discord.Interactions;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class RandomNumberCommand : LoggedCommandModule
    {
        [SlashCommand("randomnumber", "Generates a random number between the specified minimum and maximum values")]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task RandomNumberAsync(
            [Summary("min", "Minimum value (inclusive)")] int min = 1,
            [Summary("max", "Maximum value (inclusive)")] int max = 100)
        {
            await LogCommandAsync(("min", min), ("max", max));

            if (min >= max)
            {
                await RespondAsync(
                    $"Invalid range: minimum value ({min}) must be less than maximum value ({max}).",
                    ephemeral: true);
                return;
            }

            // Using Random.Shared for better thread-safety and performance
            int randomNumber = Random.Shared.Next(min, max + 1); // max is exclusive → +1

            await RespondAsync(
                $"Your random number between {min} and {max} is: **{randomNumber}**",
                ephemeral: true);
        }
    }
}