using Leaderboard.Domain;

namespace Leaderboard.Application;

public sealed record UserScoreView(
    UserId UserId,
    long WindowScore,
    long AllTimeScore,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd);

/// <summary>
/// A user who never submitted is "not found" (distinguishes a typo from a real zero);
/// a known user with no activity in the window gets <c>WindowScore = 0</c>.
/// </summary>
public sealed class GetUserScore(ILeaderboardReadStore store, LeaderboardRules rules, TimeProvider clock)
{
    public async Task<Result<UserScoreView>> HandleAsync(string? userId, CancellationToken cancellationToken)
    {
        Result<UserId> user = UserId.Create(userId);
        if (!user.IsSuccess)
        {
            return Result<UserScoreView>.Failure(user.Errors);
        }

        AggregationWindow window = AggregationWindow.EndingAt(clock.GetUtcNow(), rules);
        UserScoreSnapshot? snapshot = await store.GetUserScoreAsync(user.Value, window, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return Result<UserScoreView>.Failure(new DomainError(
                ApplicationErrors.UserNotFound,
                $"User '{user.Value}' has no recorded scores."));
        }

        return Result<UserScoreView>.Success(new UserScoreView(
            user.Value, snapshot.WindowScore, snapshot.AllTimeScore, window.Start, window.End));
    }
}
