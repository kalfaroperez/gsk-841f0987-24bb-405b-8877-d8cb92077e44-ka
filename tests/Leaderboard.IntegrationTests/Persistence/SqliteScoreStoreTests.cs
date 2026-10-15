using Dapper;
using Leaderboard.Application;
using Leaderboard.Domain;
using Microsoft.Data.Sqlite;

namespace Leaderboard.IntegrationTests.Persistence;

public sealed class SqliteScoreStoreTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly SqliteStoreFixture _db = new();

    public Task InitializeAsync() => _db.InitializeAsync();

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Initialize_is_idempotent_and_enables_wal()
    {
        await _db.Database.InitializeAsync(Ct);

        await using SqliteConnection connection = await _db.Database.OpenConnectionAsync(Ct);
        string mode = await connection.ExecuteScalarAsync<string>("PRAGMA journal_mode;") ?? "";

        Assert.Equal("wal", mode, ignoreCase: true);
    }

    [Fact]
    public async Task Append_new_event_returns_created()
    {
        Assert.Equal(AppendOutcome.Created, await _db.Store.AppendAsync(_db.Event("alice", 10), Ct));
    }

    [Fact]
    public async Task Append_same_event_twice_counts_once()
    {
        ScoreEvent e = _db.Event("alice", 10, key: "req-1");

        AppendOutcome first = await _db.Store.AppendAsync(e, Ct);
        AppendOutcome second = await _db.Store.AppendAsync(e, Ct);
        UserScoreSnapshot? score = await _db.Store.GetUserScoreAsync(e.UserId, _db.Window, Ct);

        Assert.Equal(AppendOutcome.Created, first);
        Assert.Equal(AppendOutcome.Duplicate, second);
        Assert.Equal(new UserScoreSnapshot(10, 10), score);
    }

    [Fact]
    public async Task Top_sums_inside_window_and_breaks_ties_by_user_id()
    {
        await _db.Store.AppendAsync(_db.Event("carol", 20), Ct);
        await _db.Store.AppendAsync(_db.Event("bob", 15), Ct);
        await _db.Store.AppendAsync(_db.Event("bob", 5, SqliteStoreFixture.Now.AddHours(-1)), Ct);
        await _db.Store.AppendAsync(_db.Event("alice", 20), Ct);
        await _db.Store.AppendAsync(_db.Event("alice", 500, SqliteStoreFixture.Now.AddDays(-8)), Ct);

        IReadOnlyList<LeaderboardEntry> top = await _db.Store.GetTopAsync(_db.Window, 10, Ct);

        Assert.Equal(
            [(1, "alice", 20L), (2, "bob", 20L), (3, "carol", 20L)],
            top.Select(e => (e.Rank, e.UserId.Value, e.Score)));
    }

    [Fact]
    public async Task Top_respects_the_limit()
    {
        for (int i = 0; i < 5; i++)
        {
            await _db.Store.AppendAsync(_db.Event($"u{i}", i + 1), Ct);
        }

        IReadOnlyList<LeaderboardEntry> top = await _db.Store.GetTopAsync(_db.Window, 2, Ct);

        Assert.Equal(["u4", "u3"], top.Select(e => e.UserId.Value));
    }

    [Fact]
    public async Task User_score_separates_window_from_all_time()
    {
        await _db.Store.AppendAsync(_db.Event("alice", 10), Ct);
        await _db.Store.AppendAsync(_db.Event("alice", 100, SqliteStoreFixture.Now.AddDays(-30)), Ct);

        UserScoreSnapshot? alice = await _db.Store.GetUserScoreAsync(UserId.Create("alice").Value, _db.Window, Ct);
        UserScoreSnapshot? ghost = await _db.Store.GetUserScoreAsync(UserId.Create("ghost").Value, _db.Window, Ct);

        Assert.Equal(new UserScoreSnapshot(10, 110), alice);
        Assert.Null(ghost);
    }

    [Fact]
    public async Task Sql_aggregation_matches_the_domain_reference_implementation()
    {
        var random = new Random(7);
        var accepted = new List<ScoreEvent>();
        for (int i = 0; i < 2_000; i++)
        {
            ScoreEvent e = _db.Event(
                $"u{random.Next(50)}",
                random.Next(0, 1_000),
                SqliteStoreFixture.Now.AddMinutes(-random.Next(0, 10 * 24 * 60)),
                key: $"evt-{random.Next(1_800)}"); // ~10% collisions exercise idempotency

            if (await _db.Store.AppendAsync(e, Ct) == AppendOutcome.Created)
            {
                accepted.Add(e);
            }
        }

        IReadOnlyList<LeaderboardEntry> expected = Ranking.Top(
            WindowAggregation.Totals(accepted.Select(e => new ScoreBucket(e.UserId, e.BucketStart(_db.Rules.BucketSize), e.Points)), _db.Window),
            100);

        Assert.Equal(expected, await _db.Store.GetTopAsync(_db.Window, 100, Ct));
    }
}
