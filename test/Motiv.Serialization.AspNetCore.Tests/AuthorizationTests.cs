using System.Net;

namespace Motiv.Serialization.AspNetCore.Tests;

public class AuthorizationTests
{
    [Fact]
    public async Task Should_reject_unauthenticated_requests_with_401()
    {
        // Arrange
        await using var app = await TestApp.StartAsync(Registry(), Options());
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");

        // Act
        var response = await client.GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_serve_authenticated_requests()
    {
        // Arrange
        await using var app = await TestApp.StartAsync(Registry(), Options());

        // Act
        var response = await app.GetTestClient().GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_serve_anonymous_requests_when_the_mount_site_opts_out()
    {
        // Arrange — the explicit, greppable escape at the call site
        await using var app = await TestApp.StartAsync(
            Registry(), Options(), endpointOptions: o => o.AllowAnonymous());
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");

        // Act
        var response = await client.GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static SpecRegistry Registry() => new SpecRegistry().Register(
        "customer.is-active",
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create());

    private static MotivRulesOptions Options() => new MotivRulesOptions().AddModel<Customer>("customer");

    private sealed record Customer(bool IsActive);
}
