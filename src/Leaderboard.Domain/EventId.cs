using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Leaderboard.Domain;

/// <summary>
/// Unique identity of a score event, used to make ingestion idempotent.
/// Client-supplied keys live under the "k:" namespace and derived hashes under "h:",
/// so the two sources can never collide.
/// </summary>
public readonly record struct EventId
{
    public const int MaxKeyLength = 128;

    private const string KeyPrefix = "k:";
    private const string HashPrefix = "h:";

    private EventId(string value) => Value = value;

    public string Value { get; }

    public static Result<EventId> FromIdempotencyKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length is 0 or > MaxKeyLength || !key.All(c => c is >= '!' and <= '~'))
        {
            return Result<EventId>.Failure(new DomainError(
                DomainErrors.InvalidIdempotencyKey,
                $"Idempotency-Key must be 1-{MaxKeyLength} printable ASCII characters without spaces."));
        }

        return Result<EventId>.Success(new EventId(KeyPrefix + key));
    }

    /// <summary>
    /// Deterministic id for events sent without an Idempotency-Key: retries of the same payload
    /// map to the same id. Trade-off: two genuinely distinct events with identical
    /// (user, points, instant) are treated as one.
    /// </summary>
    public static EventId Derive(UserId userId, long points, DateTimeOffset occurredAt)
    {
        string canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"v1|{userId.Value}|{points}|{occurredAt.UtcTicks}");

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return new EventId(HashPrefix + Convert.ToHexStringLower(hash));
    }

    public override string ToString() => Value;
}
