using Leaderboard.Domain;

namespace Leaderboard.UnitTests.Domain;

public class TimeBucketTests
{
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

    [Theory]
    [InlineData("2026-10-15T12:00:00Z", "2026-10-15T12:00:00Z")]
    [InlineData("2026-10-15T12:00:59.9999999Z", "2026-10-15T12:00:00Z")]
    [InlineData("2026-10-15T12:01:00Z", "2026-10-15T12:01:00Z")]
    public void StartOf_floors_to_bucket_boundary(string instant, string expected)
    {
        Assert.Equal(TestData.At(expected), TimeBucket.StartOf(TestData.At(instant), OneMinute));
    }

    [Fact]
    public void StartOf_returns_utc_regardless_of_input_offset()
    {
        DateTimeOffset bucket = TimeBucket.StartOf(TestData.At("2026-10-15T07:00:30-05:00"), OneMinute);

        Assert.Equal(TimeSpan.Zero, bucket.Offset);
        Assert.Equal(TestData.At("2026-10-15T12:00:00Z"), bucket);
    }

    [Fact]
    public void Hourly_buckets_align_to_utc_hours()
    {
        Assert.Equal(
            TestData.At("2026-10-15T12:00:00Z"),
            TimeBucket.StartOf(TestData.At("2026-10-15T12:59:59Z"), TimeSpan.FromHours(1)));
    }
}
