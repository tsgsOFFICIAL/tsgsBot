using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Services;

public sealed class MemberCounterService(DiscordSocketClient client, ILogger<MemberCounterService>? logger = null)
{

    // CONFIG
    private const ulong GuildId = 227048721710317569;
    private const ulong MemberChannelId = 604739962629521418;
    private const ulong BotChannelId = 604739965124870164;
    private const ulong CombinedChannelId = 604739960146493441;

    /// <summary>
    /// Asynchronously updates the member, bot, and combined member count channels to reflect the current number of
    /// users in the guild.
    /// </summary>
    /// <remarks>This method retrieves the latest user counts from the guild and renames the specified
    /// channels to display the current numbers. If the guild is not found or not cached, the method completes without
    /// making any changes. Channel renaming is performed asynchronously for each relevant channel.</remarks>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    public async Task UpdateAsync()
    {
        SocketGuild guild = client.GetGuild(GuildId);
        if (guild == null)
        {
            logger?.LogWarning("Guild {GuildId} not found or not cached.", GuildId);
            return;
        }

        int humans = 0;
        int bots = 0;

        await foreach (IReadOnlyCollection<IGuildUser>? userBatch in guild.GetUsersAsync())
        {
            foreach (IGuildUser user in userBatch)
            {
                if (user.IsBot)
                    bots++;
                else
                    humans++;
            }
        }

        int total = humans + bots;

        // Rename channels
        await RenameChannelAsync(MemberChannelId, $"Member{Plural(humans)}: {humans}");
        await RenameChannelAsync(BotChannelId, $"Bot{Plural(bots)}: {bots}");
        await RenameChannelAsync(CombinedChannelId, $"All member{Plural(total)}: {total}");

        logger?.LogInformation("Member counter updated → Humans: {Humans}, Bots: {Bots}, Total: {Total}", humans, bots, total);
    }

    private async Task RenameChannelAsync(ulong channelId, string newName)
    {
        IGuildChannel? channel = client.GetChannel(channelId) as IGuildChannel;
        if (channel == null)
        {
            logger?.LogWarning("Channel {ChannelId} not found.", channelId);
            return;
        }

        try
        {
            await channel.ModifyAsync(properties => properties.Name = newName);
            logger?.LogDebug("Renamed channel {ChannelId} to '{NewName}'", channelId, newName);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to rename channel {ChannelId} to '{NewName}'", channelId, newName);
        }
    }

    private static string Plural(int count) => count == 1 ? "" : "s";
}