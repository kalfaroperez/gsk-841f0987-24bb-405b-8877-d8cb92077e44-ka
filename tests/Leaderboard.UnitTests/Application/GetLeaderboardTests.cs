using Leaderboard.Application;
using Leaderboard.Domain;
using Leaderboard.UnitTests.Domain;
using Microsoft.Extensions.Time.Testing;

namespace Leaderboard.UnitTests.Application;

public class GetLeaderboardTests
{
    private static readonly LeaderboardQueryLimits Limits = new(DefaultTop: 10, MaxTop: 100);

    private readonly FakeTimeProvider _clock = new(TestData.At("2026-10-15T12:00:00Z"));
    private readonly InMemoryScoreStore _store = new(TestData.Rules);
    private readonly SubmitScore _submit;
    private readonly GetLeaderboard _sut;

    public GetLeaderboardTests()
    {
        _submit = new SubmitScore(_store, TestData.Rules, _clock);
        _sut = new GetLeaderboard(_store, TestData.Rules, Limits, _clock);
    }

    private async Task Seed(string user, long score, DateTimeOffset at) =>
        Assert.True((await _submit.HandleAsync(new SubmitScoreCommand(user, score, at, null), CancellationToken.None)).IsSuccess);

    [Fact]
    public async Task Uses_default_top_when_not_specified()
    {
        for (int i = 0; i < 15; i++)
        {
            await Seed($"u{i}", i, _clock.GetUtcNow());
        }

        Result<LeaderboardView> result = await _sut.HandleAsync(null, CancellationToken.None);

        Assert.Equal(Limits.DefaultTop, result.Value.Entries.Count);
        Assert.Equal("u14", result.Value.Entries[0].UserId.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Rejects_top_outside_limits(int top)
    {
        Result<LeaderboardView> result = await _sut.HandleAsync(top, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrors.TopOutOfRange, result.Errors[0].Code);
    }

    [Fact]
    public async Task Aggregates_only_the_configured_window_relative_to_the_clock()
    {
        await Seed("alice", 10, _clock.GetUtcNow());
        await Seed("alice", 100, _clock.GetUtcNow().AddDays(-8));
        await Seed("bob", 20, _clock.GetUtcNow().AddDays(-6));

        LeaderboardView now = (await _sut.HandleAsync(5, CancellationToken.None)).Value;
        _clock.Advance(TimeSpan.FromDays(2));
        LeaderboardView later = (await _sut.HandleAsync(5, CancellationToken.None)).Value;

        Assert.Equal([("bob", 20L), ("alice", 10L)], now.Entries.Select(e => (e.UserId.Value, e.Score)));
        Assert.Equal([("alice", 10L)], later.Entries.Select(e => (e.UserId.Value, e.Score)));
    }

    [Fact]
    public async Task View_exposes_window_bounds_and_generation_time()
    {
        LeaderboardView view = (await _sut.HandleAsync(null, CancellationToken.None)).Value;
        AggregationWindow expected = AggregationWindow.EndingAt(_clock.GetUtcNow(), TestData.Rules);

        Assert.Equal(expected.Start, view.WindowStart);
        Assert.Equal(expected.End, view.WindowEnd);
        Assert.Equal(_clock.GetUtcNow(), view.GeneratedAt);
        Assert.Empty(view.Entries);
    }
}
