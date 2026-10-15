namespace Leaderboard.Domain;

public sealed record DomainError(string Code, string Message);

/// <summary>
/// Outcome of an operation whose failure is an expected, validatable case (not exceptional).
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        Errors = [];
    }

    private Result(IReadOnlyList<DomainError> errors)
    {
        if (errors.Count == 0)
        {
            throw new ArgumentException("A failed result requires at least one error.", nameof(errors));
        }

        Errors = errors;
    }

    public bool IsSuccess => Errors.Count == 0;

    public IReadOnlyList<DomainError> Errors { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result: {Errors[0].Code}.");

#pragma warning disable CA1000 // Static factories on the generic type are the idiomatic way to build a Result.
    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(params IReadOnlyList<DomainError> errors) => new(errors);
#pragma warning restore CA1000
}
