using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Motiv.Serialization;
using Motiv.Serialization.AspNetCore;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

/// <summary>
/// Pins the host's configuration branching in Program.cs: which identity, grant source, and
/// break-glass wiring each configuration shape produces, and which shapes refuse to start.
/// </summary>
public class ConfigurationBranchTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ConfigurationBranchTests(WebApplicationFactory<Program> factory) => _factory = IsolatedStore(factory);

    [Fact]
    public async Task Should_start_the_dev_identity_in_production_when_explicitly_acknowledged()
    {
        // Arrange — the demo container's shape: release image, both flags loud and explicit
        var acknowledged = _factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("Motiv:DevIdentity:Enabled", "true")
            .UseSetting("Motiv:DevIdentity:AllowInProduction", "true"));

        // Act
        var response = await acknowledged.CreateClient().GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_challenge_unauthenticated_requests_under_the_oidc_branch()
    {
        // Arrange — dev identity off, an authority configured: the JwtBearer branch. A no-token
        // challenge never fetches OIDC metadata, so the unreachable authority is never contacted.
        var oidc = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Motiv:DevIdentity:Enabled", "false")
            .UseSetting("Motiv:Oidc:Authority", "http://localhost:9/realms/motiv")
            .UseSetting("Motiv:Grants:Path", TempPath("grants")));

        // Act
        var response = await oidc.CreateClient().GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldContain("Bearer");
    }

    [Fact]
    public void Should_register_the_claims_grant_source_when_configured()
    {
        // Arrange
        var claims = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Motiv:DevIdentity:Enabled", "false")
            .UseSetting("Motiv:Oidc:Authority", "http://localhost:9/realms/motiv")
            .UseSetting("Motiv:Grants:Source", "claims"));

        // Act & Assert
        claims.Services.GetRequiredService<IGrantSource>().ShouldBeOfType<ClaimsGrantSource>();
    }

    [Fact]
    public void Should_register_the_app_store_without_bootstrap_by_default()
    {
        // Arrange — no Motiv:Grants:Source means the app-owned store, undecorated
        var app = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Motiv:DevIdentity:Enabled", "false")
            .UseSetting("Motiv:Oidc:Authority", "http://localhost:9/realms/motiv")
            .UseSetting("Motiv:Grants:Path", TempPath("grants")));

        // Act & Assert
        app.Services.GetRequiredService<IGrantSource>().ShouldBeOfType<JsonFileGrantSource>();
    }

    [Fact]
    public void Should_wrap_the_app_store_with_bootstrap_when_a_subject_is_configured()
    {
        // Arrange
        var bootstrapped = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Motiv:DevIdentity:Enabled", "false")
            .UseSetting("Motiv:Oidc:Authority", "http://localhost:9/realms/motiv")
            .UseSetting("Motiv:Grants:Path", TempPath("grants"))
            .UseSetting("Motiv:Bootstrap:Subject", "first-admin"));

        // Act & Assert
        bootstrapped.Services.GetRequiredService<IGrantSource>().ShouldBeOfType<BootstrapGrantSource>();
    }

    [Fact]
    public void Should_refuse_an_unknown_grant_source()
    {
        // Arrange — fail loud at startup, not silently at first request
        var unknown = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Motiv:DevIdentity:Enabled", "false")
            .UseSetting("Motiv:Oidc:Authority", "http://localhost:9/realms/motiv")
            .UseSetting("Motiv:Grants:Source", "banana"));

        // Act
        var startup = () => unknown.CreateClient();

        // Assert
        startup.ShouldThrow<Exception>().Message.ShouldContain("banana");
    }

    [Fact]
    public void Should_engage_break_glass_from_configuration()
    {
        // Arrange — deploy-time config overrides AddGovernance's BreakGlass.Off default
        var breakGlass = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Motiv:BreakGlass:Enabled", "true")
            .UseSetting("Motiv:BreakGlass:ExpiresUtc", "2030-01-01T00:00:00Z"));

        // Act
        var registered = breakGlass.Services.GetRequiredService<BreakGlass>();

        // Assert
        registered.Enabled.ShouldBeTrue();
        registered.ExpiresUtc.ShouldBe(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Should_refuse_a_malformed_break_glass_expiry()
    {
        // Arrange — a typo'd expiry must not silently run break-glass without its time-box
        var malformed = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Motiv:BreakGlass:Enabled", "true")
            .UseSetting("Motiv:BreakGlass:ExpiresUtc", "not-a-date"));

        // Act
        var startup = () => malformed.CreateClient();

        // Assert
        startup.ShouldThrow<Exception>();
    }

    private static string TempPath(string prefix) =>
        Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.json");

    // Points the store at a fresh temp database rather than the sample's real motiv-store.db, which
    // every WebApplicationFactory<Program> in this assembly (and `dotnet run` itself) shares on
    // disk. StoreSchema now survives two hosts creating the schema at once, so this is no longer
    // about that crash: xunit runs test classes in parallel, and a shared store would let one
    // class's published rules and grants show up in another's assertions.
    private static WebApplicationFactory<Program> IsolatedStore(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.UseSetting(
            "Motiv:Store:ConnectionString",
            $"Data Source={Path.Combine(Path.GetTempPath(), $"motiv-{Guid.NewGuid():N}.db")}"));
}
