using Leaderboard.Domain;

namespace Leaderboard.Application;

public sealed record SubmitScoreCommand(string? UserId, long Score, DateTimeOffset Timestamp, string? IdempotencyKey);

public sealed record SubmitScoreResult(EventId EventId, AppendOutcome Outcome);

public sealed class SubmitScore(IScoreWriteStore store, LeaderboardRules rules, TimeProvider clock)
{
    public async Task<Result<SubmitScoreResult>> HandleAsync(SubmitScoreCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<ScoreEvent> created = ScoreEvent.Create(
            command.UserId, command.Score, command.Timestamp, command.IdempotencyKey, clock.GetUtcNow(), rules);
        if (!created.IsSuccess)
        {
            return Result<SubmitScoreResult>.Failure(created.Errors);
        }

        AppendOutcome outcome = await store.AppendAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result<SubmitScoreResult>.Success(new SubmitScoreResult(created.Value.Id, outcome));
    }
}
