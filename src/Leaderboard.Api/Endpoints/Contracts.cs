namespace Leaderboard.Api.Endpoints;

// Wire contracts (snake_case via the configured JSON naming policy). Kept separate from domain types
// so the public API can stay stable while the domain evolves.

/// <summary>Fields are nullable so missing values produce a 400 with field-level errors instead of a binding failure.</summary>
public sealed record ScoreRequest(string? UserId, long? Score, DateTimeOffset? Timestamp);

public sealed record ScoreAccepted(string EventId, string Status);

public sealed record LeaderboardEntryResponse(int Rank, string UserId, long Score);

public sealed record LeaderboardResponse(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<LeaderboardEntryResponse> Entries);

public sealed record UserScoreResponse(
    string UserId,
    long WindowScore,
    long AllTimeScore,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd);
