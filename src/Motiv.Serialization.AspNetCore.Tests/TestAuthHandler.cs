using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
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
    public const string SchemeName = "Test";
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

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

/// <summary>
/// Enrols the <see cref="TestAuthHandler"/> scheme on a test host. The rules endpoints are secure
/// by default, so every host that mounts them needs authentication and authorization wired up —
/// this keeps that wiring identical across the test hosts rather than repeated at each one.
/// </summary>
internal static class TestAuth
{
    public static IServiceCollection AddTestAuth(this IServiceCollection services)
    {
        services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
        return services.AddAuthorization();
    }

    public static WebApplication UseTestAuth(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
