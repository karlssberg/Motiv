using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

public class RuleDiTests
{
    private sealed record Customer(bool IsActive);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class ActiveRule() : Rule<Customer, string>("active-rule", IsActive);

    private sealed class InactiveRule() : Rule<Customer, string>("inactive-rule", !IsActive);

    private sealed class BrokenRule() : Rule<Customer, string>(
        "broken-rule", RuleDocuments.FromJson("""{ "rule": { "spec": "missing" } }"""));

    private static (SpecRegistry Registry, MotivRulesOptions Options) Fixture() =>
        (new SpecRegistry().Register("is-active", IsActive),
         new MotivRulesOptions().AddModel<Customer>("customer"));

    private static async Task<WebApplication> StartAsync(Action<MotivRulesBuilder> enroll)
    {
        var (registry, options) = Fixture();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        enroll(builder.Services.AddMotivRules(registry, options));
        var app = builder.Build();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Should_register_rules_as_singletons_and_mount_endpoints_from_di()
    {
        // Arrange
        await using var app = await StartAsync(rules => rules.AddRule<ActiveRule>());

        // Act — the injected handle and the endpoint see the same live rule
        var handle = app.Services.GetRequiredService<ActiveRule>();
        var body = await app.GetTestClient().GetFromJsonAsync<JsonElement>("/api/rules/rules");

        // Assert — one singleton RuleSet, holding the very instance the handle resolves to
        handle.Evaluate(new Customer(true)).Satisfied.ShouldBeTrue();
        body[0].GetProperty("name").GetString()!.ShouldBe("active-rule");
        var ruleSet = app.Services.GetRequiredService<RuleSet>();
        ruleSet.ShouldBeSameAs(app.Services.GetRequiredService<RuleSet>());
        ruleSet.Find("active-rule").ShouldBeSameAs(handle);
    }

    [Fact]
    public async Task Should_reflect_endpoint_updates_in_the_injected_handle()
    {
        // Arrange
        await using var app = await StartAsync(rules => rules.AddRule<ActiveRule>());
        var document = JsonDocument.Parse("""{ "rule": { "not": { "spec": "is-active" } } }""").RootElement;

        // Act — hot-swap over HTTP, observe via the injected handle
        var put = await app.GetTestClient()
            .PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 });

        // Assert
        put.EnsureSuccessStatusCode();
        app.Services.GetRequiredService<ActiveRule>()
            .Evaluate(new Customer(true)).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_enroll_an_existing_rule_instance()
    {
        // Arrange
        var rule = new ActiveRule();
        await using var app = await StartAsync(rules => rules.AddRule(rule));

        // Act
        var handle = app.Services.GetRequiredService<ActiveRule>();
        var body = await app.GetTestClient().GetFromJsonAsync<JsonElement>("/api/rules/rules");

        // Assert — the injected handle is the very instance that was enrolled
        handle.ShouldBeSameAs(rule);
        rule.Evaluate(new Customer(true)).Satisfied.ShouldBeTrue();
        body[0].GetProperty("name").GetString()!.ShouldBe("active-rule");
    }

    [Fact]
    public async Task Should_enroll_multiple_rules_and_list_them_all()
    {
        // Arrange
        await using var app = await StartAsync(rules => rules
            .AddRule<ActiveRule>()
            .AddRule<InactiveRule>());

        // Act
        var body = await app.GetTestClient().GetFromJsonAsync<JsonElement>("/api/rules/rules");

        // Assert — both handles resolve and both rules are listed
        app.Services.GetRequiredService<ActiveRule>()
            .Evaluate(new Customer(true)).Satisfied.ShouldBeTrue();
        app.Services.GetRequiredService<InactiveRule>()
            .Evaluate(new Customer(true)).Satisfied.ShouldBeFalse();
        body.EnumerateArray()
            .Select(entry => entry.GetProperty("name").GetString())
            .ShouldBe(["active-rule", "inactive-rule"], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_enroll_a_rule_from_a_base_typed_variable_without_double_registration()
    {
        // Arrange — the compile-time type is RuleBase, so AddRule infers TRule = RuleBase
        RuleBase rule = new ActiveRule();

        // Act — mapping would throw a duplicate-name error on a double enrollment
        await using var app = await StartAsync(rules => rules.AddRule(rule));
        var body = await app.GetTestClient().GetFromJsonAsync<JsonElement>("/api/rules/rules");

        // Assert — enrolled once, and the concrete-type slot still resolves
        body.GetArrayLength().ShouldBe(1);
        app.Services.GetRequiredService<ActiveRule>().ShouldBeSameAs(rule);
    }

    [Fact]
    public async Task Should_fail_at_mapping_time_when_a_document_default_is_invalid()
    {
        // Arrange — the rule's default document references an unregistered spec
        var (registry, options) = Fixture();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMotivRules(registry, options).AddRule<BrokenRule>();
        await using var app = builder.Build();

        // Act — mapping resolves the RuleSet eagerly, binding every default
        var exception = Should.Throw<RuleSerializationException>(() => app.MapMotivRules("/api/rules"));

        // Assert — the failure surfaces at startup, naming the rule that failed
        exception.Message.ShouldContain("broken-rule");
        exception.Errors.ShouldContain(error => error.Code == RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public async Task Should_explain_how_to_register_when_map_is_called_without_add()
    {
        // Arrange — AddMotivRules was never called
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => app.MapMotivRules("/api/rules"))
            .Message.ShouldContain("AddMotivRules");
    }
}
