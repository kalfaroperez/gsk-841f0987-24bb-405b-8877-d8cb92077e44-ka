using System.Buffers;

namespace Leaderboard.Domain;

/// <summary>
/// Case-sensitive user identifier. The charset is restricted so ids are safe in keys, logs and URLs
/// without escaping, and the length is bounded to cap per-key memory in caches and indexes.
/// </summary>
public readonly record struct UserId : IComparable<UserId>
{
    public const int MaxLength = 64;

    private static readonly SearchValues<char> AllowedChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-.");

    private UserId(string value) => Value = value;

    public string Value { get; }

    public static Result<UserId> Create(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length > MaxLength || raw.AsSpan().ContainsAnyExcept(AllowedChars))
        {
            return Result<UserId>.Failure(new DomainError(
                DomainErrors.InvalidUserId,
                $"user_id must be 1-{MaxLength} characters from [A-Za-z0-9_.-]."));
        }

        return Result<UserId>.Success(new UserId(raw));
    }

    public int CompareTo(UserId other) => string.CompareOrdinal(Value, other.Value);

    public static bool operator <(UserId left, UserId right) => left.CompareTo(right) < 0;

    public static bool operator <=(UserId left, UserId right) => left.CompareTo(right) <= 0;

    public static bool operator >(UserId left, UserId right) => left.CompareTo(right) > 0;

    public static bool operator >=(UserId left, UserId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value;
}
