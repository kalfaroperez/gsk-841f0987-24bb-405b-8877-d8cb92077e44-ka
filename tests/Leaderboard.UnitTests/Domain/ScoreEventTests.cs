using Leaderboard.Domain;

namespace Leaderboard.UnitTests.Domain;

public class ScoreEventTests
{
    private static readonly DateTimeOffset Now = TestData.At("2026-10-15T12:00:00Z");

    private static Result<ScoreEvent> Create(
        string? userId = "alice",
        long points = 50,
        DateTimeOffset? occurredAt = null,
        string? idempotencyKey = null) =>
        ScoreEvent.Create(userId, points, occurredAt ?? Now, idempotencyKey, Now, TestData.Rules);

    [Fact]
    public void Create_builds_a_valid_event_normalized_to_utc()
    {
        Result<ScoreEvent> result = Create(occurredAt: TestData.At("2026-10-15T06:30:00-05:00"));

        Assert.True(result.IsSuccess);
        ScoreEvent e = result.Value;
        Assert.Equal("alice", e.UserId.Value);
        Assert.Equal(50, e.Points);
        Assert.Equal(TimeSpan.Zero, e.OccurredAt.Offset);
        Assert.Equal(TestData.At("2026-10-15T11:30:00Z"), e.OccurredAt);
    }

    [Fact]
    public void Create_derives_event_id_when_no_idempotency_key()
    {
        ScoreEvent e = Create().Value;

        Assert.Equal(EventId.Derive(e.UserId, e.Points, e.OccurredAt), e.Id);
    }

    [Fact]
    public void Create_uses_idempotency_key_when_present()
    {
        ScoreEvent e = Create(idempotencyKey: "req-1").Value;

        Assert.Equal("k:req-1", e.Id.Value);
    }

    [Fact]
    public void Create_computes_bucket_from_event_time()
    {
        ScoreEvent e = Create(occurredAt: TestData.At("2026-10-15T11:59:45Z")).Value;

        Assert.Equal(TestData.At("2026-10-15T11:59:00Z"), e.BucketStart(TestData.Rules.BucketSize));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_000)]
    public void Create_accepts_points_within_range(long points)
    {
        Assert.True(Create(points: points).IsSuccess);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_001)]
    [InlineData(long.MaxValue)]
    public void Create_rejects_points_out_of_range(long points)
    {
        Result<ScoreEvent> result = Create(points: points);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrors.ScoreOutOfRange, result.Errors[0].Code);
    }

    [Fact]
    public void Create_accepts_future_timestamps_within_skew_tolerance()
    {
        Assert.True(Create(occurredAt: Now + TestData.Rules.FutureSkewTolerance).IsSuccess);
    }

    [Fact]
    public void Create_rejects_timestamps_beyond_skew_tolerance()
    {
        Result<ScoreEvent> result = Create(occurredAt: Now + TestData.Rules.FutureSkewTolerance + TimeSpan.FromTicks(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrors.TimestampInFuture, result.Errors[0].Code);
    }

    [Fact]
    public void Create_accepts_late_events_outside_the_window()
    {
        // Late events are persisted for audit/all-time totals; the window simply excludes them.
        Assert.True(Create(occurredAt: Now.AddDays(-30)).IsSuccess);
    }

    [Fact]
    public void Create_reports_all_validation_errors_at_once()
    {
        Result<ScoreEvent> result = Create(userId: "", points: -5, occurredAt: Now.AddDays(1), idempotencyKey: "bad key");

        Assert.False(result.IsSuccess);
        Assert.Equal(
            new[] { DomainErrors.InvalidUserId, DomainErrors.ScoreOutOfRange, DomainErrors.TimestampInFuture, DomainErrors.InvalidIdempotencyKey },
            result.Errors.Select(e => e.Code));
    }
}
