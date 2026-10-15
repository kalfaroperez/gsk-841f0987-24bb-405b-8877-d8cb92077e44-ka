using Leaderboard.Domain;

namespace Leaderboard.Application;

public enum AppendOutcome
{
    Created,
    Duplicate,
}

/// <summary>
/// Write side. Implementations must persist the event and its aggregates atomically and
/// count each <see cref="EventId"/> exactly once, even under concurrent calls.
/// </summary>
public interface IScoreWriteStore
{
    Task<AppendOutcome> AppendAsync(ScoreEvent scoreEvent, CancellationToken cancellationToken);
}

public sealed record UserScoreSnapshot(long WindowScore, long AllTimeScore);

/// <summary>
/// Top-N as read from the store at <see cref="AsOf"/>. Carrying the read time lets caches serve the snapshot
/// while still reporting its true age to clients. Immutable, so in-process caches can share the instance.
/// </summary>
[System.ComponentModel.ImmutableObject(true)]
public sealed record LeaderboardSnapshot(IReadOnlyList<LeaderboardEntry> Entries, DateTimeOffset AsOf);

/// <summary>
/// Read side. Each call must observe a single consistent snapshot (no partially applied writes).
/// Implementations must follow the semantics of <see cref="WindowAggregation"/> and <see cref="Ranking"/>.
/// </summary>
public interface ILeaderboardReadStore
{
    Task<LeaderboardSnapshot> GetTopAsync(AggregationWindow window, int top, CancellationToken cancellationToken);

    /// <returns><c>null</c> when the user has never submitted a score.</returns>
    Task<UserScoreSnapshot?> GetUserScoreAsync(UserId userId, AggregationWindow window, CancellationToken cancellationToken);
}
