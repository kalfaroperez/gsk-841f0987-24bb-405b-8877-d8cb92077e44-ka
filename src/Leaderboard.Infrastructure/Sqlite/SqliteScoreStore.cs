using Dapper;
using Leaderboard.Application;
using Leaderboard.Domain;
using Microsoft.Data.Sqlite;

namespace Leaderboard.Infrastructure.Sqlite;

/// <summary>
/// SQLite implementation of both ports.
/// Writes: one IMMEDIATE transaction inserts the event (dedup by primary key) and increments the aggregates
/// in SQL (<c>total = total + excluded.total</c>), never read-modify-write in application code.
/// Reads: a single statement per query, so it observes one consistent WAL snapshot.
/// </summary>
public sealed class SqliteScoreStore(SqliteDatabase database, LeaderboardRules rules, TimeProvider clock)
    : IScoreWriteStore, ILeaderboardReadStore, IDisposable
{
    private const string InsertEventSql = """
        INSERT INTO score_events (event_id, user_id, points, occurred_at, received_at)
        VALUES (@EventId, @UserId, @Points, @OccurredAt, @ReceivedAt)
        ON CONFLICT (event_id) DO NOTHING;
        """;

    private const string IncrementAggregatesSql = """
        INSERT INTO score_buckets (user_id, bucket_start, total)
        VALUES (@UserId, @BucketStart, @Points)
        ON CONFLICT (user_id, bucket_start) DO UPDATE SET total = total + excluded.total;

        INSERT INTO user_totals (user_id, total)
        VALUES (@UserId, @Points)
        ON CONFLICT (user_id) DO UPDATE SET total = total + excluded.total;
        """;

    // Ordering must match Ranking: score DESC, then user_id ASC. BINARY collation is ordinal for the ASCII-only ids.
    private const string TopSql = """
        SELECT user_id AS UserId, SUM(total) AS Score
        FROM score_buckets
        WHERE bucket_start >= @Start AND bucket_start < @End
        GROUP BY user_id
        ORDER BY Score DESC, user_id ASC
        LIMIT @Top;
        """;

    private const string UserScoreSql = """
        SELECT
            (SELECT total FROM user_totals WHERE user_id = @UserId) AS AllTime,
            (SELECT COALESCE(SUM(total), 0) FROM score_buckets
             WHERE user_id = @UserId AND bucket_start >= @Start AND bucket_start < @End) AS WindowScore;
        """;

    // SQLite allows a single writer per database. Serializing writers in-process avoids lock contention
    // and busy-wait retries; busy_timeout still protects against writers in other processes.
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public async Task<AppendOutcome> AppendAsync(ScoreEvent scoreEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scoreEvent);

        var parameters = new
        {
            EventId = scoreEvent.Id.Value,
            UserId = scoreEvent.UserId.Value,
            scoreEvent.Points,
            OccurredAt = scoreEvent.OccurredAt.UtcTicks,
            ReceivedAt = clock.GetUtcNow().UtcTicks,
            BucketStart = scoreEvent.BucketStart(rules.BucketSize).UtcTicks,
        };

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using (connection.ConfigureAwait(false))
            {
                // deferred: false => BEGIN IMMEDIATE: take the write lock up front instead of upgrading mid-transaction.
                using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);

                int inserted = await connection.ExecuteAsync(
                    new CommandDefinition(InsertEventSql, parameters, transaction, cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
                if (inserted == 0)
                {
                    return AppendOutcome.Duplicate; // disposing the transaction rolls it back; nothing was written
                }

                await connection.ExecuteAsync(
                    new CommandDefinition(IncrementAggregatesSql, parameters, transaction, cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return AppendOutcome.Created;
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<LeaderboardSnapshot> GetTopAsync(AggregationWindow window, int top, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(top, 0);

        SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            DateTimeOffset asOf = clock.GetUtcNow();
            IEnumerable<TopRow> rows = await connection.QueryAsync<TopRow>(new CommandDefinition(
                TopSql,
                new { Start = window.Start.UtcTicks, End = window.End.UtcTicks, Top = top },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            LeaderboardEntry[] entries = rows
                .Select((row, i) => new LeaderboardEntry(i + 1, UserId.Create(row.UserId).Value, row.Score))
                .ToArray();
            return new LeaderboardSnapshot(entries, asOf);
        }
    }

    public async Task<UserScoreSnapshot?> GetUserScoreAsync(UserId userId, AggregationWindow window, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);

        SqliteConnection connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            UserRow row = await connection.QuerySingleAsync<UserRow>(new CommandDefinition(
                UserScoreSql,
                new { UserId = userId.Value, Start = window.Start.UtcTicks, End = window.End.UtcTicks },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            return row.AllTime is null ? null : new UserScoreSnapshot(row.WindowScore, row.AllTime.Value);
        }
    }

    public void Dispose() => _writeGate.Dispose();

    private sealed record TopRow(string UserId, long Score);

    private sealed record UserRow(long? AllTime, long WindowScore);
}
