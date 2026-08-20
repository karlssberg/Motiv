using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

public class DevIdentityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DevIdentityTests(WebApplicationFactory<Program> factory) => _factory = IsolatedStore(factory);

    [Fact]
    public void Should_refuse_to_start_when_no_identity_is_configured()
    {
        // Arrange — Production environment, no dev identity, no OIDC
        var bare = _factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("Motiv:DevIdentity:Enabled", "false"));

        // Act
        var startup = () => bare.CreateClient();

        // Assert
        startup.ShouldThrow<Exception>().Message.ShouldContain("secure by default");
    }

    [Fact]
    public void Should_refuse_the_dev_identity_in_production_without_explicit_acknowledgement()
    {
        // Arrange
        var production = _factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("Motiv:DevIdentity:Enabled", "true"));

        // Act
        var startup = () => production.CreateClient();

        // Assert
        startup.ShouldThrow<Exception>().Message.ShouldContain("AllowInProduction");
    }

    [Fact]
    public async Task Should_authenticate_every_request_as_the_dev_principal_when_enabled()
    {
        // Arrange — default factory environment is Development; appsettings.Development.json enables it
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Points the store at a fresh temp database rather than the sample's real motiv-store.db, which
    // every WebApplicationFactory<Program> in this assembly (and `dotnet run` itself) shares on disk
    // — xunit runs test classes in parallel, and two hosts racing EnsureCreatedAsync's schema
    // creation against the same still-empty file crash with "table already exists" rather than the
    // benign no-op EnsureCreated intends for an existing schema.
    private static WebApplicationFactory<Program> IsolatedStore(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.UseSetting(
            "Motiv:Store:ConnectionString",
            $"Data Source={Path.Combine(Path.GetTempPath(), $"motiv-{Guid.NewGuid():N}.db")}"));
}
