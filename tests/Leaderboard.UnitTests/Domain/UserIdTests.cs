using Leaderboard.Domain;

namespace Leaderboard.UnitTests.Domain;

public class UserIdTests
{
    [Theory]
    [InlineData("alice")]
    [InlineData("user_01")]
    [InlineData("player-9.eu")]
    [InlineData("A")]
    public void Create_accepts_valid_ids(string raw)
    {
        Result<UserId> result = UserId.Create(raw);

        Assert.True(result.IsSuccess);
        Assert.Equal(raw, result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" alice")]
    [InlineData("al ice")]
    [InlineData("alice;drop")]
    [InlineData("ñandú")]
    [InlineData("a/b")]
    public void Create_rejects_invalid_ids(string? raw)
    {
        Result<UserId> result = UserId.Create(raw);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrors.InvalidUserId, result.Errors[0].Code);
    }

    [Fact]
    public void Create_enforces_max_length()
    {
        Assert.True(UserId.Create(new string('a', UserId.MaxLength)).IsSuccess);
        Assert.False(UserId.Create(new string('a', UserId.MaxLength + 1)).IsSuccess);
    }

    [Fact]
    public void Ids_are_case_sensitive()
    {
        Assert.NotEqual(TestData.User("Alice"), TestData.User("alice"));
    }

    [Fact]
    public void Comparison_is_ordinal()
    {
        // Ordinal: uppercase (0x42) sorts before lowercase (0x61), independent of culture.
        Assert.True(TestData.User("B").CompareTo(TestData.User("a")) < 0);
    }
}
