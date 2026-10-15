using Leaderboard.Application;
using Leaderboard.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Leaderboard.Api.Endpoints;

/// <summary>Maps domain/application errors to RFC 9457 problem responses with stable, machine-readable codes.</summary>
internal static class Problems
{
    public const string ErrorCodesExtension = "error_codes";

    public static ValidationProblem Validation(IReadOnlyList<DomainError> errors) =>
        TypedResults.ValidationProblem(
            errors.GroupBy(e => FieldOf(e.Code)).ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray()),
            title: "One or more validation errors occurred.",
            extensions: Codes(errors));

    public static ProblemHttpResult NotFound(DomainError error) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not found",
            detail: error.Message,
            extensions: Codes([error]));

    public static ValidationProblem Required(params string[] fields) =>
        TypedResults.ValidationProblem(
            fields.ToDictionary(f => f, f => new[] { $"{f} is required." }),
            title: "One or more validation errors occurred.",
            extensions: new Dictionary<string, object?> { [ErrorCodesExtension] = fields.Select(f => $"{f}.required").ToArray() });

    private static Dictionary<string, object?> Codes(IEnumerable<DomainError> errors) =>
        new() { [ErrorCodesExtension] = errors.Select(e => e.Code).ToArray() };

    private static string FieldOf(string code) => code switch
    {
        DomainErrors.InvalidIdempotencyKey => "Idempotency-Key",
        ApplicationErrors.TopOutOfRange => "top",
        _ => code[..code.IndexOf('.', StringComparison.Ordinal)],
    };
}
