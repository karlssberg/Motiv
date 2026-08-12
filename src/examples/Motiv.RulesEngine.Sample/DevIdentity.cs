using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Motiv.Serialization;

namespace Motiv.RulesEngine.Sample;

/// <summary>
/// The fail-closed dev identity: authenticates every request as a fixed dev principal so
/// `docker compose up` coexists with secure-by-default endpoints. Never active by omission.
/// </summary>
internal sealed class DevIdentityHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevIdentity";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "dev"),
                new Claim(ClaimTypes.Name, "Dev User"),
                new Claim(ClaimTypes.Role, "motiv-dev")
            ],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

/// <summary>Warns continuously while the dev identity is active — loud, never silent.</summary>
internal sealed class DevIdentityWarningService(ILogger<DevIdentityWarningService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Motiv dev identity is ACTIVE: every request is authenticated as the dev " +
                "superuser. Never enable this in a production deployment.");
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}

/// <summary>
/// Warns continuously while break-glass is configured — loud, never silent, same pattern as
/// <see cref="DevIdentityWarningService"/>. Registered only when the host's configuration turns
/// break-glass on; the every-60-seconds cadence matches the audit trail's own expectation that a
/// bypassed gate never goes quiet.
/// </summary>
internal sealed class BreakGlassWarningService(BreakGlass breakGlass, ILogger<BreakGlassWarningService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (breakGlass.ExpiresUtc is { } expires)
                logger.LogWarning(
                    "Motiv break-glass is ACTIVE: the approval gate is bypassed for every publish " +
                    "until {ExpiresUtc}. Never leave this configured longer than the incident requires.",
                    expires);
            else
                logger.LogWarning(
                    "Motiv break-glass is ACTIVE with no expiry: the approval gate is bypassed for " +
                    "every publish until this is turned off. Never leave this configured longer than " +
                    "the incident requires.");

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
