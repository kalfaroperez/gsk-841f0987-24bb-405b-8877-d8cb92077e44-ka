using Leaderboard.Domain;

namespace Leaderboard.UnitTests.Domain;

public class AggregationWindowTests
{
    private static readonly DateTimeOffset Now = TestData.At("2026-10-15T12:00:30Z");

    [Fact]
    public void Window_is_aligned_to_buckets_and_spans_exactly_the_configured_duration()
    {
        AggregationWindow window = AggregationWindow.EndingAt(Now, TestData.Rules);

        Assert.Equal(TestData.At("2026-10-15T12:01:00Z"), window.End);
        Assert.Equal(TestData.At("2026-10-08T12:01:00Z"), window.Start);
        Assert.Equal(TestData.Rules.Window, window.End - window.Start);
    }

    [Fact]
    public void Window_covers_a_fixed_number_of_buckets()
    {
        AggregationWindow window = AggregationWindow.EndingAt(Now, TestData.Rules);

        Assert.Equal(7 * 24 * 60, window.BucketCount);
    }

    [Fact]
    public void Window_includes_the_current_instant()
    {
        AggregationWindow window = AggregationWindow.EndingAt(Now, TestData.Rules);

        Assert.True(window.Contains(Now));
    }

    [Fact]
    public void Start_is_inclusive_and_end_is_exclusive()
    {
        AggregationWindow window = AggregationWindow.EndingAt(Now, TestData.Rules);

        Assert.True(window.Contains(window.Start));
        Assert.False(window.Contains(window.Start.AddTicks(-1)));
        Assert.True(window.Contains(window.End.AddTicks(-1)));
        Assert.False(window.Contains(window.End));
    }

    [Fact]
    public void Event_older_than_the_window_is_excluded()
    {
        AggregationWindow window = AggregationWindow.EndingAt(Now, TestData.Rules);

        Assert.False(window.Contains(Now.AddDays(-8)));
    }

    [Fact]
    public void ContainsBucket_matches_bucket_starts_within_range()
    {
        AggregationWindow window = AggregationWindow.EndingAt(Now, TestData.Rules);

        Assert.True(window.ContainsBucket(TestData.At("2026-10-15T12:00:00Z")));
        Assert.True(window.ContainsBucket(window.Start));
        Assert.False(window.ContainsBucket(window.Start.AddMinutes(-1)));
        Assert.False(window.ContainsBucket(window.End));
    }
}
