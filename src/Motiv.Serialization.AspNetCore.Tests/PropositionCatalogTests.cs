using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

public class PropositionCatalogTests
{
    private sealed record Customer(bool IsActive);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class DerivedRule() : Rule<Customer, string>(
        "derived-rule", RuleDocuments.FromJson("""{ "rule": { "spec": "customer.derived" } }"""));

    private static (SpecRegistry Registry, MotivRulesOptions Options) Fixture() =>
        (new SpecRegistry().Register("customer.is-active", IsActive),
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
    public async Task Should_resolve_a_proposition_set_sharing_the_rule_sets_scope()
    {
        // Arrange
        await using var app = await StartAsync(rules => rules.AddPropositions());

        // Act
        var propositions = app.Services.GetRequiredService<PropositionSet>();
        var ruleSet = app.Services.GetRequiredService<RuleSet>();

        // Assert — a shared scope is what makes the cascade atomic across both
        propositions.Create("customer.derived", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null)
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        ruleSet.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_register_every_model_with_the_proposition_set()
    {
        // Arrange — options.AddModel<T> must reach PropositionSet.AddModel<T> without reflection
        await using var app = await StartAsync(rules => rules.AddPropositions());

        // Act
        var result = app.Services.GetRequiredService<PropositionSet>()
            .Create("customer.derived", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
    }

    [Fact]
    public void Should_load_stored_propositions_before_rule_defaults_bind()
    {
        // Arrange — a rule whose *default* document references an authored proposition only binds
        // if propositions loaded first, so this pins the startup ordering.
        var store = new InMemoryPropositionStore();
        store.Save(new StoredProposition(
            "customer.derived", "customer", """{ "rule": { "spec": "customer.is-active" } }""", 1, null));

        var (registry, options) = Fixture();
        var services = new ServiceCollection();
        services.AddMotivRules(registry, options)
            .AddPropositions(store)
            .AddRule<DerivedRule>();
        var provider = services.BuildServiceProvider();

        // Act
        var resolve = () => provider.GetRequiredService<RuleSet>();

        // Assert
        resolve.ShouldNotThrow();
    }

    [Fact]
    public async Task Should_include_authored_propositions_in_the_catalog()
    {
        // Arrange — the regression guard for the catalog being a closed-over constant
        await using var app = await StartAsync(rules => rules.AddPropositions());
        var client = app.GetTestClient();

        app.Services.GetRequiredService<PropositionSet>()
            .Create("customer.derived", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Act
        var catalog = await client.GetFromJsonAsync<CatalogPeek>("/api/rules/catalog");

        // Assert
        catalog.ShouldNotBeNull();
        catalog.Specs.Select(spec => spec.Name).ShouldContain("customer.derived");
    }

    [Fact]
    public async Task Should_tag_catalog_entries_with_their_origin()
    {
        // Arrange
        await using var app = await StartAsync(rules => rules.AddPropositions());
        var client = app.GetTestClient();

        app.Services.GetRequiredService<PropositionSet>()
            .Create("customer.derived", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Act
        var catalog = await client.GetFromJsonAsync<CatalogPeek>("/api/rules/catalog");

        // Assert
        var byName = catalog!.Specs.ToDictionary(spec => spec.Name);
        byName["customer.is-active"].Origin.ShouldBe("Compiled");
        byName["customer.derived"].Origin.ShouldBe("Authored");
    }

    private sealed record CatalogPeek(IReadOnlyList<SpecPeek> Specs);

    private sealed record SpecPeek(string Name, string Origin);
}
