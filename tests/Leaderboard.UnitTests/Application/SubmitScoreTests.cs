using Leaderboard.Application;
using Leaderboard.Domain;
using Leaderboard.UnitTests.Domain;
using Microsoft.Extensions.Time.Testing;

namespace Leaderboard.UnitTests.Application;

public class SubmitScoreTests
{
    private readonly FakeTimeProvider _clock = new(TestData.At("2026-10-15T12:00:00Z"));
    private readonly InMemoryScoreStore _store;
    private readonly SubmitScore _sut;

    public SubmitScoreTests()
    {
        _store = new InMemoryScoreStore(TestData.Rules, _clock);
        _sut = new SubmitScore(_store, TestData.Rules, _clock);
    }

    private SubmitScoreCommand Command(string user = "alice", long score = 50, string? key = null) =>
        new(user, score, _clock.GetUtcNow(), key);

    [Fact]
    public async Task Valid_submission_is_created()
    {
        Result<SubmitScoreResult> result = await _sut.HandleAsync(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppendOutcome.Created, result.Value.Outcome);
        Assert.Equal(1, _store.EventCount);
    }

    [Fact]
    public async Task Retried_submission_is_reported_as_duplicate_and_not_stored_twice()
    {
        await _sut.HandleAsync(Command(), CancellationToken.None);
        Result<SubmitScoreResult> retry = await _sut.HandleAsync(Command(), CancellationToken.None);

        Assert.True(retry.IsSuccess);
        Assert.Equal(AppendOutcome.Duplicate, retry.Value.Outcome);
        Assert.Equal(1, _store.EventCount);
    }

    [Fact]
    public async Task Idempotency_key_defines_the_event_identity()
    {
        Result<SubmitScoreResult> result = await _sut.HandleAsync(Command(key: "req-1"), CancellationToken.None);

        Assert.Equal("k:req-1", result.Value.EventId.Value);
    }

    [Fact]
    public async Task Future_check_uses_the_injected_clock()
    {
        var command = new SubmitScoreCommand("alice", 5, _clock.GetUtcNow().AddMinutes(10), null);

        Result<SubmitScoreResult> rejected = await _sut.HandleAsync(command, CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(10));
        Result<SubmitScoreResult> accepted = await _sut.HandleAsync(command, CancellationToken.None);

        Assert.Equal(DomainErrors.TimestampInFuture, rejected.Errors[0].Code);
        Assert.True(accepted.IsSuccess);
    }

    [Fact]
    public async Task Invalid_submission_returns_errors_and_does_not_touch_the_store()
    {
        Result<SubmitScoreResult> result = await _sut.HandleAsync(Command(user: "", score: -1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(0, _store.EventCount);
    }
}
