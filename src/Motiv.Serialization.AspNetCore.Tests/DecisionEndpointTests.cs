using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// The decision log and its reproductions behind <c>decisions</c>, and a rule's C# behind
/// <c>rules/{name}/csharp</c> — mapped only when a decision source is registered, read under
/// <c>Read</c> on the record's rule.
/// </summary>
public class DecisionEndpointTests
{
    private sealed record Customer(bool IsActive, int Age);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class ActiveRule() : Rule<Customer, string>("active-rule", IsActive);

    /// <summary>Grants that can be narrowed after the host has been set up under full rights.</summary>
    private sealed class SwappableGrants : IGrantSource
    {
        public IReadOnlyList<NamespaceGrant> Grants { get; set; } = [new NamespaceGrant("", GrantVerb.Read), new NamespaceGrant("", GrantVerb.Author), new NamespaceGrant("", GrantVerb.Publish)];
        public bool SupportsAdministration => false;
        public IReadOnlyCollection<string> KnownRoles => [];
        public IReadOnlyList<NamespaceGrant> GrantsFor(System.Security.Claims.ClaimsPrincipal principal) => Grants;
        public bool IsAdministrator(System.Security.Claims.ClaimsPrincipal principal) => false;
    }

    /// <summary>A host whose audited rule has logged one decision, with the sink registered as the source.</summary>
    private static async Task<(WebApplication App, HttpClient Client, Guid DecisionId)> StartAsync(Action<IServiceCollection>? services = null)
    {
        var sink = new InMemoryDecisionSink();
        var registry = new SpecRegistry().Register("is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        builder.Services.AddMotivRules(registry, options)
            .AddRule<ActiveRule>()
            .AddRuleStore()
            .AddPropositions()
            .AddDecisionLog(sink, log => { log.Backpressure = DecisionBackpressure.Block; log.Capture.StoreWhole<Customer>(); })
            .AddDecisionSource(sink);
        services?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();

        var client = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "audited": true, "rule": { "spec": "is-active" } }""").RootElement;
        (await client.PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/rules/rules/active-rule/evaluate", new { model = new { isActive = true, age = 30 } })).EnsureSuccessStatusCode();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sink.Records.Count == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        return (app, client, sink.Records[0].Id);
    }

    [Fact]
    public async Task Should_list_decisions_newest_first_and_get_one_by_id()
    {
        var (app, client, id) = await StartAsync();
        await using var _ = app;

        var listed = await client.GetFromJsonAsync<JsonElement>("/api/rules/decisions?ruleName=active-rule&limit=5");
        var one = await client.GetFromJsonAsync<JsonElement>($"/api/rules/decisions/{id}");
        var missing = await client.GetAsync($"/api/rules/decisions/{Guid.NewGuid()}");

        listed.GetProperty("records").EnumerateArray().ShouldHaveSingleItem().GetProperty("id").GetGuid().ShouldBe(id);
        one.GetProperty("ruleName").GetString()!.ShouldBe("active-rule");
        one.GetProperty("input").GetProperty("kind").GetString()!.ShouldBe("Whole");
        one.GetProperty("input").GetProperty("value").GetProperty("isActive").GetBoolean().ShouldBeTrue();
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_serve_a_reproduction_with_its_c_sharp()
    {
        var (app, client, id) = await StartAsync();
        await using var _ = app;

        var reproduction = await client.GetFromJsonAsync<JsonElement>($"/api/rules/decisions/{id}/reproduction");

        reproduction.GetProperty("fidelity").GetProperty("isExact").GetBoolean().ShouldBeTrue();
        reproduction.GetProperty("replayed").GetProperty("satisfied").GetBoolean().ShouldBeTrue();
        reproduction.GetProperty("rule").GetProperty("version").GetInt32().ShouldBe(2);
        reproduction.GetProperty("model").GetProperty("kind").GetString()!.ShouldBe("Whole");
        reproduction.GetProperty("csharp").GetString()!.ShouldContain("""registry.Get<Customer>("is-active")""");
    }

    [Fact]
    public async Task Should_refuse_a_decision_the_caller_may_not_read_and_omit_it_from_the_list()
    {
        var grants = new SwappableGrants();
        var (app, client, id) = await StartAsync(services => services.AddSingleton<IGrantSource>(grants));
        await using var _ = app;
        grants.Grants = [new NamespaceGrant("other", GrantVerb.Read)];

        (await client.GetAsync($"/api/rules/decisions/{id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/rules/decisions/{id}/reproduction")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetFromJsonAsync<JsonElement>("/api/rules/decisions")).GetProperty("records").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_print_a_rule_version_as_c_sharp()
    {
        var (app, client, _) = await StartAsync();
        await using var __ = app;

        var live = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/active-rule/csharp");
        var pinned = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/active-rule/csharp?version=2");

        live.GetProperty("source").GetString()!.ShouldContain("public static class ActiveRuleRule");
        pinned.GetProperty("source").GetString()!.ShouldContain("""registry.Get<Customer>("is-active")""");
    }

    [Fact]
    public async Task Should_answer_no_content_for_a_reverted_version_and_not_found_for_the_rest()
    {
        var (app, client, _) = await StartAsync();
        await using var __ = app;

        (await client.GetAsync("/api/rules/rules/active-rule/csharp?version=1")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/rules/rules/active-rule/csharp?version=9")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/rules/rules/nope/csharp")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
