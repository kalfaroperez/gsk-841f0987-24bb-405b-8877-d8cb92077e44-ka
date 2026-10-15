using Leaderboard.Domain;

namespace Leaderboard.UnitTests.Domain;

public class LeaderboardRulesTests
{
    [Fact]
    public void Default_rules_are_valid()
    {
        LeaderboardRules rules = LeaderboardRules.Default;

        Assert.Empty(rules.Validate());
        Assert.Equal(TimeSpan.FromDays(7), rules.Window);
        Assert.Equal(TimeSpan.FromMinutes(1), rules.BucketSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    [InlineData(7)] // 7 s does not divide a day evenly
    public void Bucket_size_must_be_positive_and_divide_a_day(int seconds)
    {
        LeaderboardRules rules = LeaderboardRules.Default with { BucketSize = TimeSpan.FromSeconds(seconds) };

        Assert.NotEmpty(rules.Validate());
    }

    [Fact]
    public void Bucket_size_cannot_exceed_a_day()
    {
        LeaderboardRules rules = LeaderboardRules.Default with
        {
            BucketSize = TimeSpan.FromDays(2),
            Window = TimeSpan.FromDays(14),
        };

        Assert.NotEmpty(rules.Validate());
    }

    [Fact]
    public void Window_must_be_a_positive_multiple_of_bucket_size()
    {
        Assert.NotEmpty((LeaderboardRules.Default with { Window = TimeSpan.Zero }).Validate());
        Assert.NotEmpty((LeaderboardRules.Default with { Window = TimeSpan.FromSeconds(90) }).Validate());
        Assert.Empty((LeaderboardRules.Default with { Window = TimeSpan.FromHours(1) }).Validate());
    }

    [Fact]
    public void Skew_tolerance_cannot_be_negative()
    {
        LeaderboardRules rules = LeaderboardRules.Default with { FutureSkewTolerance = TimeSpan.FromSeconds(-1) };

        Assert.NotEmpty(rules.Validate());
    }

    [Fact]
    public void Max_points_must_be_positive()
    {
        LeaderboardRules rules = LeaderboardRules.Default with { MaxPointsPerEvent = 0 };

        Assert.NotEmpty(rules.Validate());
    }
}
