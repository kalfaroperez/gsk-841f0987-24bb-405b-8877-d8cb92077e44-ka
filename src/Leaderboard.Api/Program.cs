using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Leaderboard.Api.Configuration;
using Leaderboard.Api.Endpoints;
using Leaderboard.Api.Hosting;
using Leaderboard.Api.Observability;
using Leaderboard.Api.Security;
using Leaderboard.Application;
using Leaderboard.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
ConfigurationManager config = builder.Configuration;

// ---- Configuration (bound lazily and validated at startup: bad config fails fast, before serving traffic) ----
builder.Services.AddOptions<LeaderboardSettings>().Bind(config.GetSection(LeaderboardSettings.Section)).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<LeaderboardSettings>, LeaderboardSettingsValidator>();
builder.Services.AddOptions<RateLimitSettings>().Bind(config.GetSection(RateLimitSettings.Section)).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<RateLimitSettings>, RateLimitSettingsValidator>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<LeaderboardSettings>>().Value.ToRules());
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<LeaderboardSettings>>().Value.ToLimits());

// ---- Application + infrastructure ----
builder.Services.AddLeaderboardApplication();
builder.Services.AddLeaderboardInfrastructure(
    connectionString: sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Leaderboard")
        ?? throw new InvalidOperationException("ConnectionStrings:Leaderboard is required."),
    leaderboardCacheTtl: sp => sp.GetRequiredService<IOptions<LeaderboardSettings>>().Value.CacheTtl);
builder.Services.AddHostedService<DatabaseInitializer>();

// ---- HTTP contract ----
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["trace_id"] = Activity.Current?.TraceId.ToHexString() ?? ctx.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi();
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 16 * 1024); // payloads are tiny; reject abuse early

// ---- Security: API keys, role-based policies, per-client rate limiting ----
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddOptions<ApiKeyAuthenticationOptions>(ApiKeyAuthenticationHandler.SchemeName)
    .Bind(config.GetSection("Auth"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ApiKeyAuthenticationOptions>, ApiKeyOptionsValidator>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Roles.Write, p => p.RequireRole(Roles.Write))
    .AddPolicy(Roles.Read, p => p.RequireRole(Roles.Read));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(LeaderboardEndpoints.RateLimitPolicy, ctx =>
    {
        RateLimitSettings s = ctx.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;
        string partition = ctx.User.Identity?.IsAuthenticated == true
            ? "key:" + ctx.User.Identity.Name
            : "ip:" + ctx.Connection.RemoteIpAddress;
        return RateLimitPartition.GetTokenBucketLimiter(partition, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = s.Burst,
            TokensPerPeriod = s.PermitsPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    });
    o.OnRejected = async (ctx, token) =>
    {
        int retryAfter = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan wait)
            ? Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds))
            : 1;
        ctx.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
        await ctx.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx.HttpContext,
            ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests", Detail = $"Retry after {retryAfter}s." },
        });
    };
});

// ---- Observability: health, metrics (Prometheus), traces (OTLP when configured), structured logs ----
builder.Services.AddSingleton<LeaderboardMetrics>();
builder.Services.AddHealthChecks().AddCheck<SqliteHealthCheck>("sqlite", tags: ["ready"]);

string serviceName = config["Telemetry:ServiceName"] ?? "leaderboard-api";
string? otlpEndpoint = config["Telemetry:OtlpEndpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddMeter(LeaderboardMetrics.MeterName)
        .AddPrometheusExporter())
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    });

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
}

// ---- Pipeline ----
WebApplication app = builder.Build();

app.Use(async (ctx, next) =>
{
    string correlationId = Activity.Current?.TraceId.ToHexString() ?? ctx.TraceIdentifier;
    ctx.Response.OnStarting(() =>
    {
        ctx.Response.Headers["X-Correlation-Id"] = correlationId;
        return Task.CompletedTask;
    });
    await next(ctx);
});
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseRateLimiter(); // after authentication: partitions by API key, falls back to IP
app.UseAuthorization();

app.MapOpenApi();
app.MapPrometheusScrapingEndpoint();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapLeaderboardEndpoints();

app.Run();

/// <summary>Entry point; public so integration tests can host it with WebApplicationFactory.</summary>
public partial class Program;
