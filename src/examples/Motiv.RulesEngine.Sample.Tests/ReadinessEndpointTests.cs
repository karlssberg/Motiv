using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

/// <summary>
/// The probe a load balancer asks before sending this replica traffic.
/// </summary>
public class ReadinessEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ReadinessEndpointTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder => builder.UseSetting(
            "Motiv:Store:ConnectionString",
            $"Data Source={Path.Combine(Path.GetTempPath(), $"motiv-{Guid.NewGuid():N}.db")}"));

    [Fact]
    public async Task Should_answer_readiness_without_credentials()
    {
        // Arrange — a load balancer holds no token, and the whole HTTP surface is otherwise
        // authenticated, so an authenticated probe would fail closed on every replica.
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Should_answer_the_operator_facing_health_endpoint_too()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
