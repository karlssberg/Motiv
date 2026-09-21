using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// The decision log and its reproductions behind <c>decisions</c>, and a rule's C# behind
/// <c>rules/{name}/csharp</c> — mapped only when a decision source is registered, read under
/// <c>Read</c> on the record's rule.
/// </summary>
public class DecisionEndpointTests
{
    [Fact]
    public async Task Should_list_decisions_newest_first_and_get_one_by_id()
    {
        await using var host = await DecisionHost.StartAsync();

        var listed = await host.Http.GetFromJsonAsync<JsonElement>("/api/rules/decisions?ruleName=active-rule&limit=5");
        var one = await host.Http.GetFromJsonAsync<JsonElement>($"/api/rules/decisions/{host.DecisionId}");
        var missing = await host.Http.GetAsync($"/api/rules/decisions/{Guid.NewGuid()}");

        listed.GetProperty("records").EnumerateArray().ShouldHaveSingleItem().GetProperty("id").GetGuid().ShouldBe(host.DecisionId);
        one.GetProperty("ruleName").GetString()!.ShouldBe("active-rule");
        one.GetProperty("input").GetProperty("kind").GetString()!.ShouldBe("Whole");
        one.GetProperty("input").GetProperty("value").GetProperty("isActive").GetBoolean().ShouldBeTrue();
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_serve_a_reproduction_with_its_c_sharp()
    {
        await using var host = await DecisionHost.StartAsync();

        var reproduction = await host.Http.GetFromJsonAsync<JsonElement>($"/api/rules/decisions/{host.DecisionId}/reproduction");

        reproduction.GetProperty("fidelity").GetProperty("isExact").GetBoolean().ShouldBeTrue();
        reproduction.GetProperty("replayed").GetProperty("satisfied").GetBoolean().ShouldBeTrue();
        reproduction.GetProperty("rule").GetProperty("version").GetInt32().ShouldBe(2);
        reproduction.GetProperty("model").GetProperty("kind").GetString()!.ShouldBe("Whole");
        reproduction.GetProperty("csharp").GetString()!.ShouldContain("""registry.Get<Customer>("is-active")""");
    }

    [Fact]
    public async Task Should_refuse_a_decision_the_caller_may_not_read_and_omit_it_from_the_list()
    {
        await using var host = await DecisionHost.StartAsync();
        host.Grants.Grants = [new NamespaceGrant("other", GrantVerb.Read)];

        (await host.Http.GetAsync($"/api/rules/decisions/{host.DecisionId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await host.Http.GetAsync($"/api/rules/decisions/{host.DecisionId}/reproduction")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await host.Http.GetFromJsonAsync<JsonElement>("/api/rules/decisions")).GetProperty("records").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_print_a_rule_version_as_c_sharp()
    {
        await using var host = await DecisionHost.StartAsync();

        var live = await host.Http.GetFromJsonAsync<JsonElement>("/api/rules/rules/active-rule/csharp");
        var pinned = await host.Http.GetFromJsonAsync<JsonElement>("/api/rules/rules/active-rule/csharp?version=2");

        live.GetProperty("source").GetString()!.ShouldContain("public static class ActiveRuleRule");
        pinned.GetProperty("source").GetString()!.ShouldContain("""registry.Get<Customer>("is-active")""");
    }

    [Fact]
    public async Task Should_answer_no_content_for_a_reverted_version_and_not_found_for_the_rest()
    {
        await using var host = await DecisionHost.StartAsync();

        (await host.Http.GetAsync("/api/rules/rules/active-rule/csharp?version=1")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.Http.GetAsync("/api/rules/rules/active-rule/csharp?version=9")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.Http.GetAsync("/api/rules/rules/nope/csharp")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_answer_not_found_for_a_reproduction_of_an_unknown_decision()
    {
        await using var host = await DecisionHost.StartAsync();

        (await host.Http.GetAsync($"/api/rules/decisions/{Guid.NewGuid()}/reproduction")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_answer_service_unavailable_for_a_reproduction_when_the_host_registered_only_a_source()
    {
        // Arrange — a source registered by hand, not through AddDecisionSource, so no reproducer exists
        var sink = new InMemoryDecisionSink();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        builder.Services.AddSingleton<IDecisionSource>(sink);
        builder.Services.AddMotivRules(new SpecRegistry(), new MotivRulesOptions());
        await using var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        var record = new DecisionRecord(Guid.NewGuid(), "c", DateTimeOffset.UtcNow, "alice", "some-rule", 1, "build", [], null,
            new RuleEvaluationResult<object?>(true, "r", ["r"], [], "r", new ExplanationNode(["r"], [])));
        await sink.WriteAsync([record], default);

        var response = await app.GetTestClient().GetAsync($"/api/rules/decisions/{record.Id}/reproduction");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Should_print_version_one_of_a_document_default_rule()
    {
        await using var host = await DecisionHost.StartAsync();

        var response = await host.Http.GetAsync("/api/rules/rules/document-rule/csharp?version=1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("source").GetString()!
            .ShouldContain("""registry.Get<Customer>("is-active")""");
    }
}
