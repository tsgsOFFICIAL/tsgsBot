namespace tsgsBot_C_.Models
{
    /// <summary>
    /// Represents the data for a giveaway event stored in the database, including its configuration, status, and
    /// metadata.
    /// </summary>
    /// <param name="Id">The unique identifier for the giveaway entry in the database.</param>
    /// <param name="MessageId">The identifier of the message associated with the giveaway.</param>
    /// <param name="ChannelId">The identifier of the channel where the giveaway is hosted.</param>
    /// <param name="GuildId">The identifier of the guild (server) in which the giveaway takes place.</param>
    /// <param name="Prize">The description of the prize to be awarded to the giveaway winner(s).</param>
    /// <param name="Winners">The number of winners to be selected for the giveaway. Must be at least 1.</param>
    /// <param name="WinnerId">The identifier of the user who won the giveaway, or null if no winner has been selected.</param>
    /// <param name="ReactionEmoji">The emoji that participants must react with to enter the giveaway.</param>
    /// <param name="EndTime">The date and time when the giveaway is scheduled to end, in UTC.</param>
    /// <param name="HasEnded">true if the giveaway has concluded; otherwise, false.</param>
    /// <param name="CreatedAt">The date and time when the giveaway was created, in UTC.</param>
    /// <param name="CreatedByUserId">The identifier of the user who created the giveaway.</param>
    public record DatabaseGiveawayModel(
        int Id,
        string MessageId,
        string ChannelId,
        string GuildId,
        string Prize,
        int Winners,
        List<ulong> WinnerIds,
        string ReactionEmoji,
        DateTime EndTime,
        bool HasEnded,
        DateTime CreatedAt,
        ulong CreatedByUserId
    );
}
