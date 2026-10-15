using Leaderboard.Domain;

namespace Leaderboard.Application;

/// <summary>
/// Top-N view. Window bounds and <see cref="GeneratedAt"/> (when the data was read, which may be earlier than
/// the request if served from cache) are exposed so clients can reason about freshness.
/// </summary>
public sealed record LeaderboardView(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<LeaderboardEntry> Entries);

public sealed class GetLeaderboard(
    ILeaderboardReadStore store,
    LeaderboardRules rules,
    LeaderboardQueryLimits limits,
    TimeProvider clock)
{
    public async Task<Result<LeaderboardView>> HandleAsync(int? top, CancellationToken cancellationToken)
    {
        int n = top ?? limits.DefaultTop;
        if (n < 1 || n > limits.MaxTop)
        {
            return Result<LeaderboardView>.Failure(new DomainError(
                ApplicationErrors.TopOutOfRange,
                $"top must be between 1 and {limits.MaxTop}."));
        }

        AggregationWindow window = AggregationWindow.EndingAt(clock.GetUtcNow(), rules);
        LeaderboardSnapshot snapshot = await store.GetTopAsync(window, n, cancellationToken).ConfigureAwait(false);

        return Result<LeaderboardView>.Success(new LeaderboardView(window.Start, window.End, snapshot.AsOf, snapshot.Entries));
    }
}
