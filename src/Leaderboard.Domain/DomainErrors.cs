namespace Leaderboard.Domain;

/// <summary>Stable error codes; the API exposes them to clients, so treat them as part of the contract.</summary>
public static class DomainErrors
{
    public const string InvalidUserId = "user_id.invalid";
    public const string ScoreOutOfRange = "score.out_of_range";
    public const string TimestampInFuture = "timestamp.in_future";
    public const string InvalidIdempotencyKey = "idempotency_key.invalid";
}
