using System.Globalization;
using Leaderboard.Domain;
using Leaderboard.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Leaderboard.IntegrationTests.Persistence;

/// <summary>A real SQLite file per test (not :memory:) so WAL, locking and pooling behave as in production.</summary>
public sealed class SqliteStoreFixture : IAsyncLifetime
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-15T12:00:30Z", CultureInfo.InvariantCulture);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"leaderboard-test-{Guid.NewGuid():N}.db");

    public FakeTimeProvider Clock { get; } = new(Now);

    public LeaderboardRules Rules { get; } = LeaderboardRules.Default;

    public SqliteDatabase Database { get; private set; } = null!;

    public SqliteScoreStore Store { get; private set; } = null!;

    public AggregationWindow Window => AggregationWindow.EndingAt(Clock.GetUtcNow(), Rules);

    public async Task InitializeAsync()
    {
        Database = new SqliteDatabase($"Data Source={_path}");
        await Database.InitializeAsync(CancellationToken.None);
        Store = new SqliteScoreStore(Database, Rules, Clock);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (string file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            File.Delete(file);
        }

        return Task.CompletedTask;
    }

    public ScoreEvent Event(string user, long points, DateTimeOffset? at = null, string? key = null) =>
        ScoreEvent.Create(user, points, at ?? Now, key, Clock.GetUtcNow(), Rules).Value;
}
