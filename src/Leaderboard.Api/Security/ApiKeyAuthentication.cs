using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Leaderboard.Api.Security;

public static class Roles
{
    public const string Write = "scores:write";
    public const string Read = "scores:read";
}

public sealed class ApiKeyEntry
{
    public string Name { get; set; } = "";

    public string Key { get; set; } = "";

    public IList<string> Roles { get; } = [];
}

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public IList<ApiKeyEntry> ApiKeys { get; } = [];
}

/// <summary>
/// Authenticates the <c>X-Api-Key</c> header against configured keys. Comparison is constant-time over SHA-256
/// digests, so neither key content nor length leaks through timing. Fails closed: no configured keys, no access.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? presented = Request.Headers[HeaderName];
        if (string.IsNullOrEmpty(presented))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        byte[] presentedDigest = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
        ApiKeyEntry? match = null;
        foreach (ApiKeyEntry entry in Options.ApiKeys)
        {
            // No early exit: every configured key is compared, keeping the timing independent of which one matched.
            if (entry.Key.Length > 0
                && CryptographicOperations.FixedTimeEquals(presentedDigest, SHA256.HashData(Encoding.UTF8.GetBytes(entry.Key))))
            {
                match = entry;
            }
        }

        if (match is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, match.Name) };
        claims.AddRange(match.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(StatusCodes.Status401Unauthorized, "Unauthorized", $"A valid {HeaderName} header is required.");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(StatusCodes.Status403Forbidden, "Forbidden", "The API key is not allowed to perform this operation.");

    private async Task WriteProblemAsync(int status, string title, string detail)
    {
        Response.StatusCode = status;
        if (status == StatusCodes.Status401Unauthorized)
        {
            Response.Headers.WWWAuthenticate = SchemeName;
        }

        IProblemDetailsService problems = Context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = { Status = status, Title = title, Detail = detail },
        }).ConfigureAwait(false);
    }
}

internal sealed class ApiKeyOptionsValidator : IValidateOptions<ApiKeyAuthenticationOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiKeyAuthenticationOptions options)
    {
        if (options.ApiKeys.Any(k => string.IsNullOrWhiteSpace(k.Name) || string.IsNullOrWhiteSpace(k.Key)))
        {
            return ValidateOptionsResult.Fail("Auth:ApiKeys entries require a Name and a Key.");
        }

        if (options.ApiKeys.Select(k => k.Key).Distinct(StringComparer.Ordinal).Count() != options.ApiKeys.Count)
        {
            return ValidateOptionsResult.Fail("Auth:ApiKeys contains duplicate keys.");
        }

        return ValidateOptionsResult.Success;
    }
}
