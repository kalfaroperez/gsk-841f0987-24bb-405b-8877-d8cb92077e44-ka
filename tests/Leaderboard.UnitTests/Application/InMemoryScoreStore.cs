using Leaderboard.Application;
using Leaderboard.Domain;

namespace Leaderboard.UnitTests.Application;

/// <summary>
/// Test double built on the domain's reference aggregation, so use-case tests do not depend on SQL.
/// </summary>
internal sealed class InMemoryScoreStore(LeaderboardRules rules) : IScoreWriteStore, ILeaderboardReadStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<EventId, ScoreEvent> _events = [];

    public int EventCount
    {
        get
        {
            lock (_gate)
            {
                return _events.Count;
            }
        }
    }

    public Task<AppendOutcome> AppendAsync(ScoreEvent scoreEvent, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_events.TryAdd(scoreEvent.Id, scoreEvent) ? AppendOutcome.Created : AppendOutcome.Duplicate);
        }
    }

    public Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(AggregationWindow window, int top, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(Ranking.Top(WindowAggregation.Totals(Buckets(), window), top));
        }
    }

    public Task<UserScoreSnapshot?> GetUserScoreAsync(UserId userId, AggregationWindow window, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ScoreEvent[] mine = _events.Values.Where(e => e.UserId == userId).ToArray();
            if (mine.Length == 0)
            {
                return Task.FromResult<UserScoreSnapshot?>(null);
            }

            long windowScore = WindowAggregation.Totals(Buckets().Where(b => b.UserId == userId), window).GetValueOrDefault(userId);
            return Task.FromResult<UserScoreSnapshot?>(new UserScoreSnapshot(windowScore, mine.Sum(e => e.Points)));
        }
    }

    private IEnumerable<ScoreBucket> Buckets() =>
        _events.Values.Select(e => new ScoreBucket(e.UserId, e.BucketStart(rules.BucketSize), e.Points));
}
