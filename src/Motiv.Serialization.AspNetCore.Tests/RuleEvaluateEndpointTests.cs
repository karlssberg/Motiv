using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// <c>POST /rules/{name}/evaluate</c> evaluates the <em>live</em> rule by name — the one the
/// application is running, code-defined default included — which <c>POST /evaluate</c> cannot,
/// since that rebuilds a spec from a posted document.
/// </summary>
public class RuleEvaluateEndpointTests
{
    private sealed record Customer(bool IsActive);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> CompiledDefault { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("ok").WhenFalse("no").Create();

    private static AsyncSpecBase<Customer, string> CompiledAsyncDefault { get; } =
        Spec.BuildAsync(async (Customer c) => { await Task.Yield(); return c.IsActive; })
            .WhenTrue("screened").WhenFalse("flagged").Create();

    private sealed class CanCheckoutRule() : Rule<Customer, string>("can-checkout", CompiledDefault);

    private sealed class ScreeningRule() : AsyncRule<Customer, string>("screening", CompiledAsyncDefault);

    private sealed class NumberRule() : Rule<int, string>(
        "number", Spec.Build((int n) => n > 0).WhenTrue("positive").WhenFalse("not positive").Create());

    private static Task<WebApplication> StartAsync()
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        var rules = new RuleSet(registry).Add(new CanCheckoutRule()).Add(new ScreeningRule()).Add(new NumberRule());
        return TestApp.StartAsync(registry, options, rules);
    }

    [Fact]
    public async Task Should_evaluate_the_live_sync_rule_on_its_compiled_default()
    {
        // Arrange
        await using var app = await StartAsync();

        // Act
        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/rules/rules/can-checkout/evaluate", new { model = new { isActive = false } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("satisfied").GetBoolean().ShouldBeFalse();
        body.GetProperty("assertions")[0].GetString()!.ShouldBe("no");
        body.GetProperty("explanation").GetProperty("assertions")[0].GetString()!.ShouldBe("no");
    }

    [Fact]
    public async Task Should_evaluate_the_live_async_rule()
    {
        // Arrange
        await using var app = await StartAsync();

        // Act
        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/rules/rules/screening/evaluate", new { model = new { isActive = true } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("satisfied").GetBoolean().ShouldBeTrue();
        body.GetProperty("assertions")[0].GetString()!.ShouldBe("screened");
    }

    [Fact]
    public async Task Should_reflect_a_published_document_on_the_very_next_evaluation()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var put = await client.PutAsJsonAsync("/api/rules/rules/can-checkout",
            new { document = new { rule = new { spec = "customer.is-active" } }, baseVersion = 1 });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/rules/rules/can-checkout/evaluate", new { model = new { isActive = false } });

        // Assert — the document's text, not the compiled default's, so it was the live rule.
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("assertions")[0].GetString()!.ShouldBe("inactive");
    }

    [Fact]
    public async Task Should_return_404_for_an_unknown_rule()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/rules/rules/nope/evaluate", new { model = new { isActive = true } });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_return_400_when_the_model_is_missing()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/rules/rules/can-checkout/evaluate", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("model");
    }

    [Fact]
    public async Task Should_return_400_when_the_model_cannot_be_bound()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/rules/rules/can-checkout/evaluate", new { model = "not a customer" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Customer");
    }

    [Fact]
    public async Task Should_return_400_when_the_rules_model_type_is_not_registered()
    {
        // The number rule's model, int, was never AddModel'd — nothing can bind a model for it.
        await using var app = await StartAsync();

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/rules/rules/number/evaluate", new { model = 5 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("model type");
    }
}
