using tsgsBot_C_.Models;
using System.Text.Json;
using NpgsqlTypes;
using Npgsql;

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

            const string createPollsTableQuery = @"
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
            ";

            _dbHelper.ExecuteNonQueryAsync(createPollsTableQuery).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Performs initialization for the current instance. This method can be called to prepare the object for use.
        /// </summary>
        public void Init()
        {
            // Intentionally left blank
        }

        #region Polls
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
        #region Giveaways
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