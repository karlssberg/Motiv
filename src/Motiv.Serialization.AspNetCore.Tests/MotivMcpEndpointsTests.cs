using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// The seven tools a coding agent reaches the decision log through, over the MCP client: each
/// answers what the endpoints answer, a rule the caller may not read is not found, a reference
/// capture never carries more than its key, and the one write needs Author.
/// </summary>
public class MotivMcpEndpointsTests
{
    private sealed record Customer(bool IsActive, int Age, string? Id = null);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class ActiveRule() : Rule<Customer, string>("active-rule", IsActive);

    private sealed class SwappableGrants : IGrantSource
    {
        public IReadOnlyList<NamespaceGrant> Grants { get; set; } = [new NamespaceGrant("", GrantVerb.Read), new NamespaceGrant("", GrantVerb.Author), new NamespaceGrant("", GrantVerb.Publish)];
        public bool SupportsAdministration => false;
        public IReadOnlyCollection<string> KnownRoles => [];
        public IReadOnlyList<NamespaceGrant> GrantsFor(System.Security.Claims.ClaimsPrincipal principal) => Grants;
        public bool IsAdministrator(System.Security.Claims.ClaimsPrincipal principal) => false;
    }

    private sealed class Host : IAsyncDisposable
    {
        public required WebApplication App { get; init; }
        public required HttpClient Http { get; init; }
        public required McpClient Mcp { get; init; }
        public required Guid DecisionId { get; init; }
        public required SwappableGrants Grants { get; init; }

        public async Task<CallToolResult> CallAsync(string tool, object? args = null)
        {
            var arguments = args is null ? null : JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(args));
            return await Mcp.CallToolAsync(tool, arguments);
        }

        public async ValueTask DisposeAsync()
        {
            await Mcp.DisposeAsync();
            await App.DisposeAsync();
        }
    }

    private static async Task<Host> StartAsync(Action<DecisionLogOptions>? log = null)
    {
        var sink = new InMemoryDecisionSink();
        var grants = new SwappableGrants();
        var registry = new SpecRegistry().Register("is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        builder.Services.AddSingleton<IGrantSource>(grants);
        builder.Services.AddMotivRules(registry, options)
            .AddRule<ActiveRule>()
            .AddRuleStore()
            .AddPropositions()
            .AddScenarios()
            .AddDecisionLog(sink, o =>
            {
                o.Backpressure = DecisionBackpressure.Block;
                if (log is null) o.Capture.StoreWhole<Customer>(); else log(o);
            })
            .AddDecisionSource(sink)
            .AddMcp();
        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        app.MapMotivMcp("/mcp");
        await app.StartAsync();

        var http = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "audited": true, "rule": { "spec": "is-active" } }""").RootElement;
        (await http.PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("/api/rules/rules/active-rule/evaluate", new { model = new { isActive = true, age = 30, id = "cust-42" } })).EnsureSuccessStatusCode();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sink.Records.Count == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp") }, http, null, false);
        var mcp = await McpClient.CreateAsync(transport);
        return new Host { App = app, Http = http, Mcp = mcp, DecisionId = sink.Records[0].Id, Grants = grants };
    }

    private static JsonElement Structured(CallToolResult result)
    {
        result.IsError.ShouldNotBe(true, result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text);
        return result.StructuredContent!.Value;
    }

    private static string ErrorText(CallToolResult result)
    {
        result.IsError.ShouldBe(true);
        return result.Content.OfType<TextContentBlock>().Single().Text;
    }

    [Fact]
    public async Task Should_list_the_seven_tools()
    {
        await using var host = await StartAsync();

        var tools = await host.Mcp.ListToolsAsync();

        tools.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ShouldBe(
            ["get_decision", "get_rule", "list_decisions", "list_scenarios", "print_rule", "reproduce_decision", "save_scenario"]);
    }

    [Fact]
    public async Task Should_get_list_and_reproduce_a_decision()
    {
        await using var host = await StartAsync();

        var one = Structured(await host.CallAsync("get_decision", new { id = host.DecisionId }));
        var listed = Structured(await host.CallAsync("list_decisions", new { ruleName = "active-rule", limit = 5 }));
        var reproduced = Structured(await host.CallAsync("reproduce_decision", new { id = host.DecisionId }));

        one.GetProperty("ruleName").GetString()!.ShouldBe("active-rule");
        listed.EnumerateArray().ShouldHaveSingleItem().GetProperty("id").GetGuid().ShouldBe(host.DecisionId);
        reproduced.GetProperty("fidelity").GetProperty("isExact").GetBoolean().ShouldBeTrue();
        reproduced.GetProperty("csharp").GetString()!.ShouldContain("""registry.Get<Customer>("is-active")""");
    }

    [Fact]
    public async Task Should_get_and_print_a_rule_and_a_proposition()
    {
        await using var host = await StartAsync();
        (await host.Http.PostAsJsonAsync("/api/rules/propositions", new
        {
            name = "customer.eligible", modelType = "customer", document = new { rule = new { spec = "is-active" } }, description = (string?)null,
        })).EnsureSuccessStatusCode();

        var rule = Structured(await host.CallAsync("get_rule", new { name = "active-rule" }));
        var pinned = Structured(await host.CallAsync("get_rule", new { name = "active-rule", version = 2 }));
        var proposition = Structured(await host.CallAsync("get_rule", new { name = "customer.eligible" }));
        var printedRule = Structured(await host.CallAsync("print_rule", new { name = "active-rule" }));
        var printedProposition = Structured(await host.CallAsync("print_rule", new { name = "customer.eligible" }));

        rule.GetProperty("version").GetInt32().ShouldBe(2);
        pinned.GetProperty("document").GetProperty("audited").GetBoolean().ShouldBeTrue();
        proposition.GetProperty("modelType").GetString()!.ShouldBe("customer");
        printedRule.GetProperty("source").GetString()!.ShouldContain("public static class ActiveRuleRule");
        printedProposition.GetProperty("source").GetString()!.ShouldContain("public static class CustomerEligibleProposition");
    }

    [Fact]
    public async Task Should_answer_not_found_for_a_rule_the_caller_may_not_read_exactly_as_for_a_missing_one()
    {
        await using var host = await StartAsync();
        host.Grants.Grants = [new NamespaceGrant("other", GrantVerb.Read)];

        var unreadable = ErrorText(await host.CallAsync("print_rule", new { name = "active-rule" }));
        var missing = ErrorText(await host.CallAsync("print_rule", new { name = "nonexistent" }));
        var unreadableDecision = ErrorText(await host.CallAsync("get_decision", new { id = host.DecisionId }));
        var missingDecision = ErrorText(await host.CallAsync("get_decision", new { id = Guid.NewGuid() }));

        unreadable.Replace("active-rule", "X").ShouldBe(missing.Replace("nonexistent", "X"));
        unreadableDecision.Replace(host.DecisionId.ToString(), "X").ShouldBe(System.Text.RegularExpressions.Regex.Replace(missingDecision, "[0-9a-f-]{36}", "X"));
        Structured(await host.CallAsync("list_decisions", new { })).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_return_only_the_key_for_an_unresolved_reference_capture()
    {
        await using var host = await StartAsync(o => o.Capture.ReferenceOnly<Customer>(c => c.Id ?? "anonymous"));

        var reproduced = Structured(await host.CallAsync("reproduce_decision", new { id = host.DecisionId }));

        reproduced.GetProperty("model").GetProperty("kind").GetString()!.ShouldBe("Reference");
        reproduced.GetProperty("model").GetProperty("key").GetString()!.ShouldBe("cust-42");
        reproduced.GetProperty("model").GetProperty("value").ValueKind.ShouldBe(JsonValueKind.Null);
        reproduced.GetProperty("decision").GetProperty("input").GetProperty("value").ValueKind.ShouldBe(JsonValueKind.Null);
        reproduced.GetProperty("fidelity").GetProperty("notes").EnumerateArray().ShouldContain(n => n.GetProperty("reason").GetString() == "ModelUnresolved");
    }

    [Fact]
    public async Task Should_save_and_list_scenarios_and_refuse_save_scenario_without_author()
    {
        await using var host = await StartAsync();

        var saved = Structured(await host.CallAsync("save_scenario", new { rule = "active-rule", name = "Active adult", model = """{ "isActive": true, "age": 30 }""", expectedSatisfied = true, sourceDecisionId = host.DecisionId.ToString() }));
        var listed = Structured(await host.CallAsync("list_scenarios", new { rule = "active-rule" }));
        host.Grants.Grants = [new NamespaceGrant("", GrantVerb.Read)];
        var refused = ErrorText(await host.CallAsync("save_scenario", new { rule = "active-rule", name = "Second", model = "{}" }));
        var still = Structured(await host.CallAsync("list_scenarios", new { rule = "active-rule" }));

        saved.GetProperty("version").GetInt32().ShouldBe(1);
        listed.EnumerateArray().ShouldHaveSingleItem().GetProperty("expectedSatisfied").GetBoolean().ShouldBeTrue();
        refused.ShouldContain("author");
        still.GetArrayLength().ShouldBe(1);
    }
}
