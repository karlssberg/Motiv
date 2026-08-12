using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// Authenticates every request by default (so the existing endpoint tests keep passing without
/// opting in to anything), unless <see cref="AnonymousHeader"/> is present, in which case
/// authentication is skipped entirely — letting <c>RequireAuthorization</c> reject the request.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    // `new` is required: AuthenticationHandler<TOptions> declares its own `Scheme` property
    // (the resolved AuthenticationScheme), so this constant deliberately hides it — the repo
    // builds with TreatWarningsAsErrors, and CS0108 would otherwise fail the build.
    public new const string Scheme = "Test";
    public const string AnonymousHeader = "X-Test-Anonymous";
    public const string SubjectHeader = "X-Test-User";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey(AnonymousHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var subject = Request.Headers.TryGetValue(SubjectHeader, out var user)
            ? user.ToString()
            : "test-user";
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, subject) };
        if (Request.Headers.TryGetValue(RolesHeader, out var roles))
            claims.AddRange(roles.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(role => new Claim(ClaimTypes.Role, role.Trim())));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme)));
    }
}
