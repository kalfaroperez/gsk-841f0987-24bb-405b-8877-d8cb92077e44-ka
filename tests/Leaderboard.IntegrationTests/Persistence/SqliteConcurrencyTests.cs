using Leaderboard.Application;
using Leaderboard.Domain;

namespace Leaderboard.IntegrationTests.Persistence;

/// <summary>
/// Evidence for the "no lost updates / no inconsistent state" requirement, against a real SQLite file.
/// All writes target the same user: the hottest possible key.
/// </summary>
public sealed class SqliteConcurrencyTests : IAsyncLifetime
{
    private const int Writers = 1_000;
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly UserId Alice = UserId.Create("alice").Value;
    private readonly SqliteStoreFixture _db = new();

    public Task InitializeAsync() => _db.InitializeAsync();

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Concurrent_writes_to_the_same_user_lose_no_updates()
    {
        await Parallel.ForEachAsync(
            Enumerable.Range(0, Writers),
            new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = Ct },
            async (i, ct) => await _db.Store.AppendAsync(_db.Event("alice", 3, key: $"w-{i}"), ct));

        UserScoreSnapshot? score = await _db.Store.GetUserScoreAsync(Alice, _db.Window, Ct);

        Assert.Equal(new UserScoreSnapshot(Writers * 3, Writers * 3), score);
    }

    [Fact]
    public async Task Concurrent_retries_of_the_same_event_are_counted_exactly_once()
    {
        ScoreEvent e = _db.Event("alice", 7, key: "same-request");
        var outcomes = new System.Collections.Concurrent.ConcurrentBag<AppendOutcome>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 500),
            new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = Ct },
            async (_, ct) => outcomes.Add(await _db.Store.AppendAsync(e, ct)));

        Assert.Equal(1, outcomes.Count(o => o == AppendOutcome.Created));
        Assert.Equal(new UserScoreSnapshot(7, 7), await _db.Store.GetUserScoreAsync(Alice, _db.Window, Ct));
    }

    [Fact]
    public async Task Readers_never_observe_a_partially_applied_write()
    {
        // Every event lands inside the window, so in any consistent snapshot window == all-time.
        // A torn read (bucket updated but total not yet, or vice versa) would break the equality.
        using var writersDone = new CancellationTokenSource();
        var violations = 0;

        Task readers = Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            while (!writersDone.IsCancellationRequested)
            {
                UserScoreSnapshot? s = await _db.Store.GetUserScoreAsync(Alice, _db.Window, Ct);
                if (s is not null && s.WindowScore != s.AllTimeScore)
                {
                    Interlocked.Increment(ref violations);
                }
            }
        }, Ct)));

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 300),
            new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = Ct },
            async (i, ct) => await _db.Store.AppendAsync(_db.Event("alice", 1, key: $"r-{i}"), ct));
        await writersDone.CancelAsync();
        await readers;

        Assert.Equal(0, violations);
        Assert.Equal(new UserScoreSnapshot(300, 300), await _db.Store.GetUserScoreAsync(Alice, _db.Window, Ct));
    }
}
