namespace Leaderboard.Application;

/// <summary>Stable error codes owned by the application layer (part of the API contract).</summary>
public static class ApplicationErrors
{
    public const string TopOutOfRange = "top.out_of_range";
    public const string UserNotFound = "user.not_found";
}
