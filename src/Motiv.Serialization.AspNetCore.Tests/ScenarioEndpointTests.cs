using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// A rule's scenarios behind <c>rules/{name}/scenarios</c>: created and replaced under a version
/// compare-and-set, listed under <c>Read</c>, written under <c>Author</c>, and served by the store
/// rather than the rule set — a scenario may precede its rule.
/// </summary>
public class ScenarioEndpointTests
{
    private sealed record Customer(bool IsActive, int Age);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static async Task<WebApplication> StartAsync(Action<IServiceCollection>? services = null)
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        builder.Services.AddMotivRules(registry, options).AddScenarios();
        services?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> Put(
        HttpClient client, string rule, string id, string name, string model, int baseVersion,
        bool? expected = null, string? source = null) =>
        client.PutAsJsonAsync($"/api/rules/rules/{rule}/scenarios/{id}", new
        {
            name, model, expectedSatisfied = expected, sourceDecisionId = source, baseVersion,
        });

    [Fact]
    public async Task Should_create_list_update_and_delete_a_rules_scenarios()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "alice");

        // Act & Assert — create
        var created = await Put(client, "can-checkout", "s1", "Active adult", """{ "age": 30 }""", 0, expected: true, source: "d-1");
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt32().ShouldBe(1);

        // list — the model comes back as the text it was sent, and the principal is the author
        var listed = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/can-checkout/scenarios");
        var entry = listed.EnumerateArray().ShouldHaveSingleItem();
        entry.GetProperty("id").GetString()!.ShouldBe("s1");
        entry.GetProperty("model").GetString()!.ShouldBe("""{ "age": 30 }""");
        entry.GetProperty("expectedSatisfied").GetBoolean().ShouldBeTrue();
        entry.GetProperty("sourceDecisionId").GetString()!.ShouldBe("d-1");
        entry.GetProperty("author").GetString()!.ShouldBe("alice");

        // update at the observed version, with a half-typed model kept verbatim
        var updated = await Put(client, "can-checkout", "s1", "Renamed", "{ \"age\": 3", 1);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);

        // a stale base version is a 409 naming the current one
        var stale = await Put(client, "can-checkout", "s1", "Stale", "{}", 1);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("currentVersion").GetInt32().ShouldBe(2);

        // delete at the observed version
        var deleted = await client.DeleteAsync("/api/rules/rules/can-checkout/scenarios/s1?baseVersion=2");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/can-checkout/scenarios")).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_answer_400_for_a_missing_name_or_a_negative_base_version()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        (await Put(client, "r", "s1", "", "{}", 0)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Put(client, "r", "s1", "x", "{}", -1)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.DeleteAsync("/api/rules/rules/r/scenarios/s1?baseVersion=0")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_never_list_a_host_bookkeeping_row()
    {
        // Arrange — ids beginning with "__" are reserved for the host (Studio's seed marker)
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var store = app.Services.GetRequiredService<IScenarioStore>();
        await store.PutAsync(new StoredScenario("r", "__seeded", "seeded", "{}", null, null, 0, "system", default), 0, default);
        (await Put(client, "r", "s1", "Visible", "{}", 0)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var listed = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/r/scenarios");

        // Assert
        listed.EnumerateArray().ShouldHaveSingleItem().GetProperty("id").GetString()!.ShouldBe("s1");
    }

    [Fact]
    public async Task Should_refuse_to_write_a_host_bookkeeping_id()
    {
        // Arrange — a write under a reserved id would succeed and then never be listed
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act & Assert
        (await Put(client, "r", "__seeded", "seeded", "{}", 0)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.DeleteAsync("/api/rules/rules/r/scenarios/__seeded?baseVersion=1")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await app.Services.GetRequiredService<IScenarioStore>().ForRuleAsync("r", default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_refuse_a_put_without_author_grant()
    {
        // Arrange — a caller who may read pricing.* but not author it
        await using var app = await StartAsync(services => services.AddSingleton<IGrantSource>(
            new FixedGrants([new NamespaceGrant("pricing", GrantVerb.Read)])));
        var client = app.GetTestClient();

        // Act & Assert
        (await Put(client, "pricing.vat", "s1", "x", "{}", 0)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.DeleteAsync("/api/rules/rules/pricing.vat/scenarios/s1?baseVersion=1")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/rules/rules/pricing.vat/scenarios")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_refuse_a_list_without_read_grant()
    {
        await using var app = await StartAsync(services => services.AddSingleton<IGrantSource>(
            new FixedGrants([new NamespaceGrant("pricing", GrantVerb.Read)])));
        var client = app.GetTestClient();

        (await client.GetAsync("/api/rules/rules/fraud.screen/scenarios")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Should_not_map_the_routes_when_no_scenario_store_is_registered()
    {
        // Arrange — the rules API without AddScenarios: the client treats this 404 as "no store"
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        builder.Services.AddMotivRules(registry, new MotivRulesOptions().AddModel<Customer>("customer"));
        await using var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();

        // Act & Assert
        (await app.GetTestClient().GetAsync("/api/rules/rules/r/scenarios")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>A grant source that answers the same grants for every principal.</summary>
    private sealed class FixedGrants(IReadOnlyList<NamespaceGrant> grants) : IGrantSource
    {
        public bool SupportsAdministration => false;
        public IReadOnlyCollection<string> KnownRoles => [];
        public IReadOnlyList<NamespaceGrant> GrantsFor(ClaimsPrincipal principal) => grants;
        public bool IsAdministrator(ClaimsPrincipal principal) => false;
    }
}
