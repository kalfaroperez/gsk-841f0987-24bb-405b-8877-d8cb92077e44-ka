using System.Globalization;
using Leaderboard.Domain;

namespace Leaderboard.UnitTests.Domain;

internal static class TestData
{
    public static LeaderboardRules Rules { get; } = LeaderboardRules.Default;

    public static DateTimeOffset At(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static UserId User(string value) => UserId.Create(value).Value;
}
