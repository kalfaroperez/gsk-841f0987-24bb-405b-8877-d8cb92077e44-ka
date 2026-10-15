using Leaderboard.Application;
using Leaderboard.Domain;
using Leaderboard.UnitTests.Domain;
using Microsoft.Extensions.Time.Testing;

namespace Leaderboard.UnitTests.Application;

public class GetUserScoreTests
{
    private readonly FakeTimeProvider _clock = new(TestData.At("2026-10-15T12:00:00Z"));
    private readonly InMemoryScoreStore _store;
    private readonly SubmitScore _submit;
    private readonly GetUserScore _sut;

    public GetUserScoreTests()
    {
        _store = new InMemoryScoreStore(TestData.Rules, _clock);
        _submit = new SubmitScore(_store, TestData.Rules, _clock);
        _sut = new GetUserScore(_store, TestData.Rules, _clock);
    }

    [Fact]
    public async Task Returns_window_and_all_time_scores()
    {
        await _submit.HandleAsync(new SubmitScoreCommand("alice", 10, _clock.GetUtcNow(), null), CancellationToken.None);
        await _submit.HandleAsync(new SubmitScoreCommand("alice", 100, _clock.GetUtcNow().AddDays(-30), null), CancellationToken.None);

        UserScoreView view = (await _sut.HandleAsync("alice", CancellationToken.None)).Value;

        Assert.Equal("alice", view.UserId.Value);
        Assert.Equal(10, view.WindowScore);
        Assert.Equal(110, view.AllTimeScore);
        Assert.Equal(AggregationWindow.EndingAt(_clock.GetUtcNow(), TestData.Rules).Start, view.WindowStart);
    }

    [Fact]
    public async Task Known_user_without_recent_activity_has_zero_window_score()
    {
        await _submit.HandleAsync(new SubmitScoreCommand("alice", 100, _clock.GetUtcNow().AddDays(-30), null), CancellationToken.None);

        UserScoreView view = (await _sut.HandleAsync("alice", CancellationToken.None)).Value;

        Assert.Equal(0, view.WindowScore);
        Assert.Equal(100, view.AllTimeScore);
    }

    [Fact]
    public async Task Unknown_user_is_not_found()
    {
        Result<UserScoreView> result = await _sut.HandleAsync("ghost", CancellationToken.None);

        Assert.Equal(ApplicationErrors.UserNotFound, result.Errors[0].Code);
    }

    [Fact]
    public async Task Invalid_user_id_is_a_validation_error()
    {
        Result<UserScoreView> result = await _sut.HandleAsync("bad id", CancellationToken.None);

        Assert.Equal(DomainErrors.InvalidUserId, result.Errors[0].Code);
    }
}
