using Dapper;
using Microsoft.Data.Sqlite;

namespace Leaderboard.Infrastructure.Sqlite;

/// <summary>Connection factory and schema owner. Connections are pooled by Microsoft.Data.Sqlite.</summary>
public sealed class SqliteDatabase
{
    // Instants are stored as UTC ticks (INTEGER): exact, compact, and compared numerically.
    // CHECK(typeof(total) = 'integer') turns SQLite's silent INTEGER→REAL overflow into a hard error.
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS score_events (
            event_id    TEXT    NOT NULL PRIMARY KEY,
            user_id     TEXT    NOT NULL,
            points      INTEGER NOT NULL,
            occurred_at INTEGER NOT NULL,
            received_at INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS score_buckets (
            user_id      TEXT    NOT NULL,
            bucket_start INTEGER NOT NULL,
            total        INTEGER NOT NULL CHECK (typeof(total) = 'integer'),
            PRIMARY KEY (user_id, bucket_start)
        ) WITHOUT ROWID;

        -- Covering index for the leaderboard query: range scan on bucket_start, no table lookups.
        CREATE INDEX IF NOT EXISTS ix_score_buckets_window ON score_buckets (bucket_start, user_id, total);

        CREATE TABLE IF NOT EXISTS user_totals (
            user_id TEXT    NOT NULL PRIMARY KEY,
            total   INTEGER NOT NULL CHECK (typeof(total) = 'integer')
        ) WITHOUT ROWID;
        """;

    private readonly string _connectionString;

    public SqliteDatabase(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString)
        {
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        };
        _connectionString = builder.ToString();
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Per-connection settings (pooled connections keep them, re-applying is cheap):
            // busy_timeout: wait on the native busy handler instead of failing when another process holds the write lock.
            // synchronous=FULL: a committed transaction survives power loss, which backs the "durable after 2xx" guarantee.
            await connection.ExecuteAsync(new CommandDefinition(
                "PRAGMA busy_timeout = 5000; PRAGMA synchronous = FULL;",
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Idempotent: enables WAL (persistent per database file) and creates the schema if missing.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            // WAL: readers never block the writer and see a consistent snapshot while a write is in progress.
            await connection.ExecuteAsync(new CommandDefinition("PRAGMA journal_mode = WAL;", cancellationToken: cancellationToken))
                .ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition(Schema, cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }
    }
}
