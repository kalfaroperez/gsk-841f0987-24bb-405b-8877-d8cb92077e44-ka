namespace Leaderboard.Domain;

/// <summary>An accepted score submission. Instances are only created through <see cref="Create"/>, so they are always valid.</summary>
public sealed record ScoreEvent
{
    private ScoreEvent(EventId id, UserId userId, long points, DateTimeOffset occurredAt)
    {
        Id = id;
        UserId = userId;
        Points = points;
        OccurredAt = occurredAt;
    }

    public EventId Id { get; }

    public UserId UserId { get; }

    public long Points { get; }

    /// <summary>Event time in UTC. The aggregation window is evaluated against this, not ingestion time.</summary>
    public DateTimeOffset OccurredAt { get; }

    public DateTimeOffset BucketStart(TimeSpan bucketSize) => TimeBucket.StartOf(OccurredAt, bucketSize);

    /// <summary>
    /// Validates every field and reports all errors at once. Late events (older than the window) are accepted:
    /// they still count towards all-time totals, and the window excludes them on read.
    /// </summary>
    public static Result<ScoreEvent> Create(
        string? userId,
        long points,
        DateTimeOffset occurredAt,
        string? idempotencyKey,
        DateTimeOffset now,
        LeaderboardRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var errors = new List<DomainError>();

        Result<UserId> user = UserId.Create(userId);
        errors.AddRange(user.Errors);

        if (points < 0 || points > rules.MaxPointsPerEvent)
        {
            errors.Add(new DomainError(
                DomainErrors.ScoreOutOfRange,
                $"score must be between 0 and {rules.MaxPointsPerEvent}."));
        }

        DateTimeOffset utc = occurredAt.ToUniversalTime();
        if (utc > now + rules.FutureSkewTolerance)
        {
            errors.Add(new DomainError(
                DomainErrors.TimestampInFuture,
                $"timestamp cannot be more than {rules.FutureSkewTolerance} ahead of server time."));
        }

        Result<EventId>? keyed = idempotencyKey is null ? null : EventId.FromIdempotencyKey(idempotencyKey);
        if (keyed is not null)
        {
            errors.AddRange(keyed.Errors);
        }

        if (errors.Count > 0)
        {
            return Result<ScoreEvent>.Failure(errors);
        }

        EventId id = keyed?.Value ?? EventId.Derive(user.Value, points, utc);
        return Result<ScoreEvent>.Success(new ScoreEvent(id, user.Value, points, utc));
    }
}
