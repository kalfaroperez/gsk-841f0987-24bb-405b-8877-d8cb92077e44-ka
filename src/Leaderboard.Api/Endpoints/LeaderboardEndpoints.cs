using Leaderboard.Api.Observability;
using Leaderboard.Api.Security;
using Leaderboard.Application;
using Leaderboard.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Leaderboard.Api.Endpoints;

public static class LeaderboardEndpoints
{
    public const string RateLimitPolicy = "per-client";

    public static IEndpointRouteBuilder MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder api = app.MapGroup("").RequireRateLimiting(RateLimitPolicy);

        api.MapPost("/score", PostScore)
            .RequireAuthorization(Roles.Write)
            .WithName("SubmitScore")
            .WithSummary("Registers a score event. Idempotent per Idempotency-Key (or per payload when absent).");

        api.MapGet("/leaderboard", GetLeaderboard)
            .RequireAuthorization(Roles.Read)
            .WithName("GetLeaderboard")
            .WithSummary("Top N users by score summed over the configured time window.");

        api.MapGet("/user/{id}/score", GetUserScore)
            .RequireAuthorization(Roles.Read)
            .WithName("GetUserScore")
            .WithSummary("Window and all-time score of a user.");

        return app;
    }

    private static async Task<Results<Created<ScoreAccepted>, Ok<ScoreAccepted>, ValidationProblem>> PostScore(
        ScoreRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        SubmitScore useCase,
        LeaderboardMetrics metrics,
        CancellationToken cancellationToken)
    {
        string[] missing =
        [
            .. request.UserId is null ? ["user_id"] : Array.Empty<string>(),
            .. request.Score is null ? ["score"] : Array.Empty<string>(),
            .. request.Timestamp is null ? ["timestamp"] : Array.Empty<string>(),
        ];
        if (missing.Length > 0)
        {
            metrics.ScoreRejected();
            return Problems.Required(missing);
        }

        Result<SubmitScoreResult> result = await useCase.HandleAsync(
            new SubmitScoreCommand(request.UserId, request.Score!.Value, request.Timestamp!.Value, idempotencyKey),
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            metrics.ScoreRejected();
            return Problems.Validation(result.Errors);
        }

        metrics.ScoreAccepted(result.Value.Outcome);
        if (result.Value.Outcome == AppendOutcome.Duplicate)
        {
            return TypedResults.Ok(new ScoreAccepted(result.Value.EventId.Value, "duplicate"));
        }

        return TypedResults.Created(
            $"/user/{Uri.EscapeDataString(request.UserId!)}/score",
            new ScoreAccepted(result.Value.EventId.Value, "created"));
    }

    private static async Task<Results<Ok<LeaderboardResponse>, ValidationProblem>> GetLeaderboard(
        int? top,
        GetLeaderboard useCase,
        CancellationToken cancellationToken)
    {
        Result<LeaderboardView> result = await useCase.HandleAsync(top, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return Problems.Validation(result.Errors);
        }

        LeaderboardView view = result.Value;
        return TypedResults.Ok(new LeaderboardResponse(
            view.WindowStart,
            view.WindowEnd,
            view.GeneratedAt,
            view.Entries.Select(e => new LeaderboardEntryResponse(e.Rank, e.UserId.Value, e.Score)).ToArray()));
    }

    private static async Task<Results<Ok<UserScoreResponse>, ValidationProblem, ProblemHttpResult>> GetUserScore(
        string id,
        GetUserScore useCase,
        CancellationToken cancellationToken)
    {
        Result<UserScoreView> result = await useCase.HandleAsync(id, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            DomainError error = result.Errors[0];
            return error.Code == ApplicationErrors.UserNotFound ? Problems.NotFound(error) : Problems.Validation(result.Errors);
        }

        UserScoreView view = result.Value;
        return TypedResults.Ok(new UserScoreResponse(
            view.UserId.Value, view.WindowScore, view.AllTimeScore, view.WindowStart, view.WindowEnd));
    }
}
