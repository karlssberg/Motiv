using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

public class DevIdentityTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void Should_refuse_to_start_when_no_identity_is_configured()
    {
        // Arrange — Production environment, no dev identity, no OIDC
        var bare = factory.WithWebHostBuilder(builder => builder
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
        var production = factory.WithWebHostBuilder(builder => builder
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
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
