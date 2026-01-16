using Discord;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;
using tsgsBot_C_.Models;

namespace tsgsBot_C_.Services
{
    public sealed class DatabaseService
    {
        private static readonly Lazy<DatabaseService> _instance = new(() => new DatabaseService());

        /// <summary>
        /// Gets the singleton instance of the database service.
        /// </summary>
        /// <remarks>This property provides global access to the shared database service instance. The
        /// instance is lazily initialized and is guaranteed to be thread-safe.</remarks>
        public static DatabaseService Instance => _instance.Value;

        private readonly DatabaseHelper _dbHelper;

        private DatabaseService()
        {
            _dbHelper = new DatabaseHelper();

            string createTablesQuery = @"
                CREATE TABLE IF NOT EXISTS polls (
                    id SERIAL PRIMARY KEY,
                    message_id TEXT UNIQUE NOT NULL,
                    channel_id TEXT NOT NULL,
                    guild_id TEXT NOT NULL,
                    question TEXT NOT NULL,
                    answers JSONB NOT NULL,
                    emojis JSONB NOT NULL,
                    end_time TIMESTAMP NOT NULL,
                    has_ended BOOLEAN DEFAULT FALSE,
                    created_at TIMESTAMP DEFAULT (TIMEZONE('UTC', NOW())),
                    created_by NUMERIC NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_polls_active 
                    ON polls (has_ended, end_time) 
                    WHERE has_ended = FALSE;

                CREATE TABLE IF NOT EXISTS giveaways (
                    id SERIAL PRIMARY KEY,
                    message_id TEXT UNIQUE NOT NULL,
                    channel_id TEXT NOT NULL,
                    guild_id TEXT NOT NULL,
                    prize TEXT NOT NULL,
                    winners INT NOT NULL DEFAULT 1,
                    winner_ids JSONB,
                    reaction_emoji TEXT NOT NULL DEFAULT '🎟️',
                    end_time TIMESTAMP NOT NULL,
                    has_ended BOOLEAN DEFAULT FALSE,
                    created_at TIMESTAMP DEFAULT (TIMEZONE('UTC', NOW())),
                    created_by NUMERIC NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_giveaways_active
                    ON giveaways (has_ended, end_time)
                    WHERE has_ended = FALSE;

                CREATE TABLE IF NOT EXISTS secrets (
                    id SERIAL PRIMARY KEY,
                    key TEXT NOT NULL,
                    value TEXT NOT NULL,
                    created_at TIMESTAMP DEFAULT (TIMEZONE('UTC', NOW()))
                );
            ";

            _dbHelper.ExecuteNonQueryAsync(createTablesQuery).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Performs initialization for the current instance. This method can be called to prepare the object for use.
        /// </summary>
        public void Init()
        {
            // Intentionally left blank
        }

        #region TABLE [Polls]
        /// <summary>
        /// Creates a new poll entry in the database.
        /// </summary>
        /// <param name="messageId">The Discord message ID.</param>
        /// <param name="channelId">The Discord channel ID.</param>
        /// <param name="guildId">The Discord guild ID.</param>
        /// <param name="question">The poll question.</param>
        /// <param name="answers">List of answer options.</param>
        /// <param name="emojis">List of emojis corresponding to answers.</param>
        /// <param name="endTime">The poll end time (in UTC).</param>
        /// <returns>The ID of the newly created poll.</returns>
        public async Task<int> CreatePollAsync(string messageId, string channelId, string guildId, string question, List<string> answers, List<string> emojis, DateTime endTime, ulong createdByUserId)
        {
            string answersJson = JsonSerializer.Serialize(answers);
            string emojisJson = JsonSerializer.Serialize(emojis);

            const string query = @"
                INSERT INTO polls (message_id, channel_id, guild_id, question, answers, emojis, end_time, created_by)
                VALUES (@messageId, @channelId, @guildId, @question, @answers, @emojis, @endTime, @createdByUserId)
                RETURNING id;";

            NpgsqlParameter[] parameters = new NpgsqlParameter[]
            {
                new("@messageId", messageId),
                new("@channelId", channelId),
                new("@guildId", guildId),
                new("@question", question),
                new("@answers", NpgsqlDbType.Jsonb)
                {
                    Value = answersJson
                },
                new("@emojis", NpgsqlDbType.Jsonb)
                {
                    Value = emojisJson
                },
                new("@endTime", DateTime.SpecifyKind(endTime, DateTimeKind.Unspecified)),
                new("@createdByUserId", NpgsqlDbType.Numeric)
                {
                    Value = (decimal)createdByUserId
                }
            };

            object? result = await _dbHelper.ExecuteScalarAsync(query, parameters);
            return result is int id ? id : throw new InvalidOperationException("Failed to retrieve poll ID.");
        }
        /// <summary>
        /// Retrieves a single poll by its ID.
        /// </summary>
        /// <param name="id">The poll ID.</param>
        /// <returns>The poll if found; otherwise, null.</returns>
        public async Task<DatabasePollModel?> GetPollAsync(int id)
        {
            const string query = "SELECT * FROM polls WHERE id = @id;";

            NpgsqlParameter[] parameters = new NpgsqlParameter[] {
                new("@id", id)
            };

            using NpgsqlDataReader reader = await _dbHelper.ExecuteReaderAsync(query, parameters);
            if (await reader.ReadAsync())
            {
                return new DatabasePollModel(
                    reader.GetInt32(reader.GetOrdinal("id")),
                    reader.GetString(reader.GetOrdinal("message_id")),
                    reader.GetString(reader.GetOrdinal("channel_id")),
                    reader.GetString(reader.GetOrdinal("guild_id")),
                    reader.GetString(reader.GetOrdinal("question")),
                    JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("answers"))) ?? new List<string>(),
                    JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("emojis"))) ?? new List<string>(),
                    reader.GetDateTime(reader.GetOrdinal("end_time")),
                    reader.GetBoolean(reader.GetOrdinal("has_ended")),
                    reader.GetDateTime(reader.GetOrdinal("created_at")),
                    Convert.ToUInt64(reader.GetValue(reader.GetOrdinal("created_by")))
                );
            }

            return null;
        }
        /// <summary>
        /// Retrieves all active (non-ended) polls.
        /// </summary>
        /// <returns>A list of active polls.</returns>
        public async Task<List<DatabasePollModel>> GetActivePollsAsync()
        {
            const string query = "SELECT * FROM polls WHERE has_ended = FALSE;";

            List<DatabasePollModel> polls = new List<DatabasePollModel>();

            using NpgsqlDataReader reader = await _dbHelper.ExecuteReaderAsync(query);
            while (await reader.ReadAsync())
            {
                polls.Add(new DatabasePollModel(
                    reader.GetInt32(reader.GetOrdinal("id")),
                    reader.GetString(reader.GetOrdinal("message_id")),
                    reader.GetString(reader.GetOrdinal("channel_id")),
                    reader.GetString(reader.GetOrdinal("guild_id")),
                    reader.GetString(reader.GetOrdinal("question")),
                    JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("answers"))) ?? new List<string>(),
                    JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("emojis"))) ?? new List<string>(),
                    reader.GetDateTime(reader.GetOrdinal("end_time")),
                    reader.GetBoolean(reader.GetOrdinal("has_ended")),
                    reader.GetDateTime(reader.GetOrdinal("created_at")),
                    Convert.ToUInt64(reader.GetValue(reader.GetOrdinal("created_by")))
                ));
            }

            return polls;
        }
        /// <summary>
        /// Updates the 'has_ended' status of a poll.
        /// </summary>
        /// <param name="id">The poll ID.</param>
        /// <param name="hasEnded">The new ended status (default: true).</param>
        public async Task UpdatePollEndedAsync(int id, bool hasEnded = true)
        {
            const string query = "UPDATE polls SET has_ended = @hasEnded WHERE id = @id;";

            NpgsqlParameter[] parameters = new NpgsqlParameter[]
            {
                new("@id", id),
                new("@hasEnded", hasEnded)
            };

            await _dbHelper.ExecuteNonQueryAsync(query, parameters);
        }
        /// <summary>
        /// Deletes a poll by its ID.
        /// </summary>
        /// <param name="id">The poll ID.</param>
        public async Task DeletePollAsync(int id)
        {
            const string query = "DELETE FROM polls WHERE id = @id;";

            NpgsqlParameter[] parameters = new NpgsqlParameter[] {
                new("@id", id)
            };

            await _dbHelper.ExecuteNonQueryAsync(query, parameters);
        }
        #endregion

        #region TABLE [Giveaways]
        /// <summary>
        /// Creates a new giveaway entry with the specified details and returns the unique identifier of the created
        /// giveaway.
        /// </summary>
        /// <param name="messageId">The unique identifier of the message associated with the giveaway.</param>
        /// <param name="channelId">The unique identifier of the channel where the giveaway is hosted.</param>
        /// <param name="guildId">The unique identifier of the guild (server) in which the giveaway is created.</param>
        /// <param name="prize">The description of the prize to be awarded to the giveaway winner(s).</param>
        /// <param name="reactionEmoji">The emoji that participants must react with to enter the giveaway.</param>
        /// <param name="endTime">The date and time when the giveaway ends. The time should be specified in UTC.</param>
        /// <param name="createdByUserId">The unique identifier of the user who created the giveaway.</param>
        /// <param name="winners">The number of winners to select for the giveaway. Must be at least 1. Defaults to 1 if not specified.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier of the
        /// newly created giveaway.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the giveaway could not be created or the giveaway ID could not be retrieved from the database.</exception>
        public async Task<int> CreateGiveawayAsync(string messageId, string channelId, string guildId, string prize, string reactionEmoji, DateTime endTime, ulong createdByUserId, int winners = 1)
        {
            const string query = @"
                INSERT INTO giveaways (message_id, channel_id, guild_id, prize, reaction_emoji, winners, end_time, created_by)
                VALUES (@messageId, @channelId, @guildId, @prize, @reaction_emoji, @winners, @endTime, @createdByUserId)
                RETURNING id;";

            NpgsqlParameter[] parameters = new NpgsqlParameter[]
            {
                new("@messageId", messageId),
                new("@channelId", channelId),
                new("@guildId", guildId),
                new("@prize", prize),
                new("@reaction_emoji", reactionEmoji),
                new("@winners", winners),
                new("@endTime", DateTime.SpecifyKind(endTime, DateTimeKind.Unspecified)),
                new("@createdByUserId", NpgsqlDbType.Numeric)
                {
                    Value = (decimal)createdByUserId
                }
            };

            object? result = await _dbHelper.ExecuteScalarAsync(query, parameters);
            return result is int id ? id : throw new InvalidOperationException("Failed to retrieve giveaway ID.");
        }
        /// <summary>
        /// Asynchronously retrieves a giveaway record from the database by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the giveaway to retrieve.</param>
        /// <returns>A <see cref="DatabaseGiveawayModel"/> representing the giveaway if found; otherwise, <see langword="null"/>.</returns>
        public async Task<DatabaseGiveawayModel?> GetGiveawayAsync(int id)
        {
            const string query = "SELECT * FROM giveaways WHERE id = @id;";
            NpgsqlParameter[] parameters = new NpgsqlParameter[] {
                new("@id", id)
            };

            using NpgsqlDataReader reader = await _dbHelper.ExecuteReaderAsync(query, parameters);

            if (await reader.ReadAsync())
            {
                return new DatabaseGiveawayModel(
                    reader.GetInt32(reader.GetOrdinal("id")),
                    reader.GetString(reader.GetOrdinal("message_id")),
                    reader.GetString(reader.GetOrdinal("channel_id")),
                    reader.GetString(reader.GetOrdinal("guild_id")),
                    reader.GetString(reader.GetOrdinal("prize")),
                    reader.GetInt32(reader.GetOrdinal("winners")),
                    JsonSerializer.Deserialize<List<ulong>>(reader.IsDBNull(reader.GetOrdinal("winner_ids")) ? "[]" : reader.GetString(reader.GetOrdinal("winner_ids"))) ?? new List<ulong>(),
                    reader.GetString(reader.GetOrdinal("reaction_emoji")),
                    reader.GetDateTime(reader.GetOrdinal("end_time")),
                    reader.GetBoolean(reader.GetOrdinal("has_ended")),
                    reader.GetDateTime(reader.GetOrdinal("created_at")),
                    Convert.ToUInt64(reader.GetValue(reader.GetOrdinal("created_by")))
                );
            }

            return null;
        }
        /// <summary>
        /// Asynchronously retrieves all active giveaways from the database.
        /// </summary>
        /// <remarks>This method queries the database for giveaways where the end condition has not been
        /// met. The operation is performed asynchronously and may involve network or I/O latency depending on the
        /// database connection.</remarks>
        /// <returns>A list of <see cref="DatabaseGiveawayModel"/> objects representing giveaways that have not ended. The list
        /// is empty if there are no active giveaways.</returns>
        public async Task<List<DatabaseGiveawayModel>> GetActiveGiveawaysAsync()
        {
            const string query = "SELECT * FROM giveaways WHERE has_ended = FALSE;";
            List<DatabaseGiveawayModel> giveaways = new List<DatabaseGiveawayModel>();

            using NpgsqlDataReader reader = await _dbHelper.ExecuteReaderAsync(query);

            while (await reader.ReadAsync())
            {
                giveaways.Add(new DatabaseGiveawayModel(
                    reader.GetInt32(reader.GetOrdinal("id")),
                    reader.GetString(reader.GetOrdinal("message_id")),
                    reader.GetString(reader.GetOrdinal("channel_id")),
                    reader.GetString(reader.GetOrdinal("guild_id")),
                    reader.GetString(reader.GetOrdinal("prize")),
                    reader.GetInt32(reader.GetOrdinal("winners")),
                    JsonSerializer.Deserialize<List<ulong>>(reader.IsDBNull(reader.GetOrdinal("winner_ids")) ? "[]" : reader.GetString(reader.GetOrdinal("winner_ids"))) ?? new List<ulong>(),
                    reader.GetString(reader.GetOrdinal("reaction_emoji")),
                    reader.GetDateTime(reader.GetOrdinal("end_time")),
                    reader.GetBoolean(reader.GetOrdinal("has_ended")),
                    reader.GetDateTime(reader.GetOrdinal("created_at")),
                    Convert.ToUInt64(reader.GetValue(reader.GetOrdinal("created_by")))
                ));
            }

            return giveaways;
        }
        /// <summary>
        /// Updates the status of a giveaway to indicate whether it has ended and optionally sets the list of winner
        /// IDs.
        /// </summary>
        /// <param name="id">The unique identifier of the giveaway to update.</param>
        /// <param name="winnerIds">A list of user IDs representing the winners of the giveaway, or null to leave the winner list unchanged.</param>
        /// <param name="hasEnded">A value indicating whether the giveaway has ended. The default is <see langword="true"/>.</param>
        /// <returns>A task that represents the asynchronous update operation.</returns>
        public async Task UpdateGiveawayEndedAsync(int id, List<ulong>? winnerIds = null, bool hasEnded = true)
        {
            const string query = "UPDATE giveaways SET has_ended = @hasEnded, winner_ids = @winnerIds WHERE id = @id;";
            NpgsqlParameter[] parameters = new NpgsqlParameter[]
            {
                new("@id", id),
                new("@winnerIds", NpgsqlDbType.Jsonb)
                {
                    Value = winnerIds != null ? JsonSerializer.Serialize(winnerIds) : DBNull.Value
                },
                new("@hasEnded", hasEnded)
            };

            await _dbHelper.ExecuteNonQueryAsync(query, parameters);
        }
        /// <summary>
        /// Deletes the giveaway entry with the specified identifier from the data store asynchronously.
        /// </summary>
        /// <remarks>If no giveaway with the specified identifier exists, the method completes without
        /// throwing an exception.</remarks>
        /// <param name="id">The unique identifier of the giveaway to delete.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        public async Task DeleteGiveawayAsync(int id)
        {
            const string query = "DELETE FROM giveaways WHERE id = @id;";
            NpgsqlParameter[] parameters = new NpgsqlParameter[] {
                new("@id", id)
            };

            await _dbHelper.ExecuteNonQueryAsync(query, parameters);
        }
        #endregion
    }

    /// <summary>
    /// Provides asynchronous helper methods for executing SQL commands and queries against a PostgreSQL database.
    /// </summary>
    /// <remarks>This class simplifies common database operations such as executing non-query commands,
    /// retrieving data readers, and obtaining scalar values. All methods use the connection string specified within the
    /// class and are designed for use with PostgreSQL via the Npgsql library. The methods are intended for asynchronous
    /// usage and automatically manage database connections. This class is not thread-safe; create separate instances if
    /// used concurrently across multiple threads.</remarks>
    public class DatabaseHelper
    {
        private readonly string _connectionString = string.Empty;

        /// <summary>
        /// Initializes a new instance of the DatabaseHelper class using the connection string specified in the
        /// DB_CONNECTION_STRING environment variable.
        /// </summary>
        /// <remarks>Ensure that the DB_CONNECTION_STRING environment variable is configured before
        /// creating an instance of DatabaseHelper. This constructor is typically used to centralize database connection
        /// management based on environment configuration.</remarks>
        /// <exception cref="InvalidOperationException">Thrown if the DB_CONNECTION_STRING environment variable is not set or is empty.</exception>
        public DatabaseHelper()
        {
            _connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ?? throw new InvalidOperationException("Connection string missing!");
        }

        /// <summary>
        /// Executes a SQL command that does not return any data, such as INSERT, UPDATE, or DELETE statements.
        /// </summary>
        /// <param name="query">The SQL query to execute.</param>
        /// <param name="parameters">An optional array of SQL parameters to include in the query.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ExecuteNonQueryAsync(string query, NpgsqlParameter[]? parameters = null)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    if (parameters != null)
                        command.Parameters.AddRange(parameters);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        /// <summary>
        /// Executes a SQL query and returns a data reader to read the result set.
        /// </summary>
        /// <param name="query">The SQL query to execute.</param>
        /// <param name="parameters">An optional array of SQL parameters to include in the query.</param>
        /// <returns>A <see cref="NpgsqlDataReader"/> for reading the query results asynchronously.</returns>
        public async Task<NpgsqlDataReader> ExecuteReaderAsync(string query, NpgsqlParameter[]? parameters = null)
        {
            NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                if (parameters != null)
                    command.Parameters.AddRange(parameters);

                return await command.ExecuteReaderAsync(System.Data.CommandBehavior.CloseConnection);
            }
        }
        /// <summary>
        /// Executes a SQL query that returns a single value, such as a SELECT COUNT(*) query.
        /// </summary>
        /// <param name="query">The SQL query to execute.</param>
        /// <param name="parameters">An optional array of SQL parameters to include in the query.</param>
        /// <returns>The single value result of the query as an object.</returns>
        public async Task<object?> ExecuteScalarAsync(string query, NpgsqlParameter[]? parameters = null)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    if (parameters != null)
                        command.Parameters.AddRange(parameters);

                    return await command.ExecuteScalarAsync();
                }
            }
        }
    }
}