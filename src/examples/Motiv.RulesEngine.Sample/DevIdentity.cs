using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

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
