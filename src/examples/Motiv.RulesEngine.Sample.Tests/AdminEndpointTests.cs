using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Motiv.Serialization.AspNetCore;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

public class AdminEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Should_report_capabilities_for_the_dev_identity()
    {
        // Arrange — default factory: Development env, dev identity + dev grant source
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/capabilities");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("administrator").GetBoolean().ShouldBeTrue();
        body.GetProperty("grantAdministration").GetBoolean().ShouldBeFalse();
        body.GetProperty("devIdentity").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_not_expose_a_grants_surface_for_the_immutable_dev_source()
    {
        // Arrange — the dev grant source cannot be administered
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/grants");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_replace_the_dev_grant_source_with_the_app_store_and_round_trip_a_grant()
    {
        // Arrange — swap in a JsonFileGrantSource seeded so "dev" (still the authenticated
        // principal) is an administrator of the app store
        var path = TempPath();
        var store = new JsonFileGrantSource(path);
        store.Add(new GrantRecord("dev", "", "administer"));

        await using var appStoreFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IGrantSource>(store)));
        var client = appStoreFactory.CreateClient();

        // Confirm last-registration-wins: GetRequiredService resolves the app store, not the dev source
        appStoreFactory.Services.GetRequiredService<IGrantSource>().ShouldBeSameAs(store);

        // Act — round trip a grant through POST then GET
        var post = await client.PostAsJsonAsync("/api/admin/grants",
            new { subject = "alice", prefix = "pricing", verb = "author" });
        var get = await client.GetAsync("/api/admin/grants");

        // Assert
        post.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        var grants = await get.Content.ReadFromJsonAsync<JsonElement>();
        grants.EnumerateArray()
            .Select(g => (g.GetProperty("subject").GetString(), g.GetProperty("prefix").GetString(), g.GetProperty("verb").GetString()))
            .ShouldContain(("alice", "pricing", "author"));
    }

    [Fact]
    public async Task Should_refuse_deleting_the_last_administer_with_a_conflict()
    {
        // Arrange
        var path = TempPath();
        var store = new JsonFileGrantSource(path);
        var admin = new GrantRecord("dev", "", "administer");
        store.Add(admin);

        await using var appStoreFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IGrantSource>(store)));
        var client = appStoreFactory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/admin/grants")
        {
            Content = JsonContent.Create(new { subject = "dev", prefix = "", verb = "administer" })
        };
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().ShouldNotBeNullOrWhiteSpace();
        store.IsAdministrator(DevPrincipal()).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_forbid_grant_administration_for_a_non_administrator()
    {
        // Arrange — app store exists (so the surface is not 404), but "dev" is not seeded as admin
        var path = TempPath();
        var store = new JsonFileGrantSource(path);

        await using var appStoreFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IGrantSource>(store)));
        var client = appStoreFactory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/grants");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"admin-grants-{Guid.NewGuid():N}.json");

    private static System.Security.Claims.ClaimsPrincipal DevPrincipal() =>
        new(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "dev")], "test"));
}
