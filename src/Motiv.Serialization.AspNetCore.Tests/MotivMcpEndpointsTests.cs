using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// The seven tools a coding agent reaches the decision log through, over the MCP client: each
/// answers what the endpoints answer, a rule the caller may not read is not found, a reference
/// capture never carries more than its key, and the one write needs Author.
/// </summary>
public class MotivMcpEndpointsTests
{
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
        await using var host = await DecisionHost.StartAsync();

        var tools = await host.Mcp.ListToolsAsync();

        tools.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ShouldBe(
            ["get_decision", "get_rule", "list_decisions", "list_scenarios", "print_rule", "reproduce_decision", "save_scenario"]);
    }

    [Fact]
    public async Task Should_get_list_and_reproduce_a_decision()
    {
        await using var host = await DecisionHost.StartAsync();

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
        await using var host = await DecisionHost.StartAsync();
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
    public async Task Should_serve_version_one_of_a_document_default_rule_as_its_document()
    {
        await using var host = await DecisionHost.StartAsync();

        var row = Structured(await host.CallAsync("get_rule", new { name = "document-rule", version = 1 }));
        var printed = Structured(await host.CallAsync("print_rule", new { name = "document-rule", version = 1 }));

        row.GetProperty("document").GetProperty("rule").GetProperty("spec").GetString()!.ShouldBe("is-active");
        printed.GetProperty("source").GetString()!.ShouldContain("""registry.Get<Customer>("is-active")""");
    }

    [Fact]
    public async Task Should_answer_not_found_for_a_rule_the_caller_may_not_read_exactly_as_for_a_missing_one()
    {
        await using var host = await DecisionHost.StartAsync();
        host.Grants.Grants = [new NamespaceGrant("other", GrantVerb.Read)];

        var unreadable = ErrorText(await host.CallAsync("print_rule", new { name = "active-rule" }));
        var missing = ErrorText(await host.CallAsync("print_rule", new { name = "nonexistent" }));
        var unreadableDecision = ErrorText(await host.CallAsync("get_decision", new { id = host.DecisionId }));
        var missingDecision = ErrorText(await host.CallAsync("get_decision", new { id = Guid.NewGuid() }));

        unreadable.Replace("active-rule", "X").ShouldBe(missing.Replace("nonexistent", "X"));
        unreadableDecision.Replace(host.DecisionId.ToString(), "X").ShouldBe(Regex.Replace(missingDecision, "[0-9a-f-]{36}", "X"));
        Structured(await host.CallAsync("list_decisions", new { })).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_return_only_the_key_for_an_unresolved_reference_capture()
    {
        await using var host = await DecisionHost.StartAsync(
            o => o.Capture.ReferenceOnly<DecisionHost.Customer>(c => c.Id ?? "anonymous"));

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
        await using var host = await DecisionHost.StartAsync();

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

    [Fact]
    public async Task Should_refuse_a_scenario_without_a_name_or_a_model()
    {
        await using var host = await DecisionHost.StartAsync();

        ErrorText(await host.CallAsync("save_scenario", new { rule = "active-rule", name = " ", model = "{}" })).ShouldContain("needs a name");
        ErrorText(await host.CallAsync("save_scenario", new { rule = "active-rule", name = "Named", model = (string?)null })).ShouldContain("needs a model");
    }

    [Fact]
    public async Task Should_answer_the_versions_a_rule_or_proposition_never_had_and_print_a_stored_proposition_version()
    {
        await using var host = await DecisionHost.StartAsync();
        (await host.Http.PostAsJsonAsync("/api/rules/propositions", new
        {
            name = "customer.eligible", modelType = "customer", document = new { rule = new { spec = "is-active" } }, description = (string?)null,
        })).EnsureSuccessStatusCode();
        (await host.Http.PutAsJsonAsync("/api/rules/propositions/customer.eligible", new
        {
            document = new { rule = new { not = new { spec = "is-active" } } }, baseVersion = 1,
        })).EnsureSuccessStatusCode();

        ErrorText(await host.CallAsync("get_rule", new { name = "active-rule", version = 9 })).ShouldContain("has no version 9");
        ErrorText(await host.CallAsync("print_rule", new { name = "active-rule", version = 9 })).ShouldContain("has no version 9");
        ErrorText(await host.CallAsync("print_rule", new { name = "active-rule", version = 1 })).ShouldContain("compiled default");
        ErrorText(await host.CallAsync("get_rule", new { name = "customer.eligible", version = 9 })).ShouldContain("has no version 9");
        ErrorText(await host.CallAsync("print_rule", new { name = "customer.eligible", version = 9 })).ShouldContain("has no version 9");
        var first = Structured(await host.CallAsync("print_rule", new { name = "customer.eligible", version = 1 }));
        first.GetProperty("source").GetString()!.ShouldContain("""registry.Get<Customer>("is-active")""");
        first.GetProperty("source").GetString()!.ShouldNotContain(".Not()");
    }
}
