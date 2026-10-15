using Leaderboard.Domain;

namespace Leaderboard.UnitTests.Domain;

public class EventIdTests
{
    private static readonly UserId Alice = TestData.User("alice");
    private static readonly DateTimeOffset Noon = TestData.At("2026-10-15T12:00:00Z");

    [Fact]
    public void FromIdempotencyKey_uses_key_namespace()
    {
        Result<EventId> result = EventId.FromIdempotencyKey("req-123");

        Assert.True(result.IsSuccess);
        Assert.Equal("k:req-123", result.Value.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("clé")]
    [InlineData("tab\tkey")]
    public void FromIdempotencyKey_rejects_invalid_keys(string key)
    {
        Result<EventId> result = EventId.FromIdempotencyKey(key);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrors.InvalidIdempotencyKey, result.Errors[0].Code);
    }

    [Fact]
    public void FromIdempotencyKey_enforces_max_length()
    {
        Assert.True(EventId.FromIdempotencyKey(new string('x', EventId.MaxKeyLength)).IsSuccess);
        Assert.False(EventId.FromIdempotencyKey(new string('x', EventId.MaxKeyLength + 1)).IsSuccess);
    }

    [Fact]
    public void Derive_is_deterministic()
    {
        EventId first = EventId.Derive(Alice, 50, Noon);
        EventId second = EventId.Derive(Alice, 50, Noon);

        Assert.Equal(first, second);
        Assert.StartsWith("h:", first.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Derive_changes_when_any_field_changes()
    {
        EventId baseline = EventId.Derive(Alice, 50, Noon);

        Assert.NotEqual(baseline, EventId.Derive(TestData.User("bob"), 50, Noon));
        Assert.NotEqual(baseline, EventId.Derive(Alice, 51, Noon));
        Assert.NotEqual(baseline, EventId.Derive(Alice, 50, Noon.AddTicks(1)));
    }

    [Fact]
    public void Derive_normalizes_offsets_to_the_same_instant()
    {
        DateTimeOffset sameInstantInBogota = TestData.At("2026-10-15T07:00:00-05:00");

        Assert.Equal(EventId.Derive(Alice, 50, Noon), EventId.Derive(Alice, 50, sameInstantInBogota));
    }

    [Fact]
    public void Key_and_hash_namespaces_never_collide()
    {
        EventId derived = EventId.Derive(Alice, 50, Noon);
        string rawHash = derived.Value["h:".Length..];

        Assert.NotEqual(derived, EventId.FromIdempotencyKey(rawHash).Value);
    }
}
