using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

public class PropositionCatalogTests
{
    /// <summary>The document authored as <c>customer.derived</c>, referencing the compiled spec.</summary>
    private const string DerivedDocument = """{ "rule": { "spec": "customer.is-active" } }""";

    /// <summary>A rule document referencing the authored proposition <c>customer.derived</c>.</summary>
    private const string DerivedRuleDocument = """{ "rule": { "spec": "customer.derived" } }""";

    private sealed record Customer(bool IsActive);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class DerivedRule() : Rule<Customer, string>(
        "derived-rule", RuleDocuments.FromJson(DerivedRuleDocument));

    /// <summary>
    /// Enrolled on a compiled default so it binds at startup even before anything is authored;
    /// <see cref="Should_resolve_a_proposition_set_sharing_the_rule_sets_scope"/> updates it at
    /// runtime to reference an authored proposition.
    /// </summary>
    private sealed class PlaceholderRule() : Rule<Customer, string>("placeholder-rule", IsActive);

    private static Task<PropositionUpdateResult> AuthorDerived(IServiceProvider services) =>
        services.GetRequiredService<PropositionSet>()
            .CreateAsync("customer.derived", "customer", DerivedDocument, null);

    private static (SpecRegistry Registry, MotivRulesOptions Options) Fixture() =>
        (new SpecRegistry().Register("customer.is-active", IsActive),
         new MotivRulesOptions().AddModel<Customer>("customer"));

    private static async Task<WebApplication> StartAsync(Action<MotivRulesBuilder> enroll)
    {
        var (registry, options) = Fixture();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        enroll(builder.Services.AddMotivRules(registry, options));
        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Should_resolve_a_proposition_set_sharing_the_rule_sets_scope()
    {
        // Arrange — a rule enrolled on a compiled default (so startup never sees "customer.derived").
        // Updating it afterward to reference a proposition authored at runtime only binds if
        // RuleSet and PropositionSet publish into the very same BindingScope overlay — a private,
        // unshared scope would leave "customer.derived" invisible to the RuleSet's serializer and
        // the update would fail as an unknown spec, not merely "not throw".
        await using var app = await StartAsync(rules => rules.AddPropositions().AddRule<PlaceholderRule>());
        var ruleSet = app.Services.GetRequiredService<RuleSet>();
        (await AuthorDerived(app.Services)).Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        // Act
        var update = await ruleSet.UpdateAsync(
            "placeholder-rule", DerivedRuleDocument, expectedVersion: 1, new RuleChangeProvenance("test"));

        // Assert — resolves, and the live rule now evaluates through the shared scope
        update.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        app.Services.GetRequiredService<PlaceholderRule>()
            .Evaluate(new Customer(true)).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_register_every_model_with_the_proposition_set()
    {
        // Arrange — options.AddModel<T> must reach PropositionSet.AddModel<T> without reflection
        await using var app = await StartAsync(rules => rules.AddPropositions());

        // Act
        var result = await AuthorDerived(app.Services);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
    }

    [Fact]
    public async Task Should_load_stored_propositions_before_rule_defaults_bind()
    {
        // Arrange — a rule whose *default* document references an authored proposition only binds
        // if propositions loaded first, so this pins the startup ordering.
        var store = new InMemoryPropositionStore();
        await store.WriteAsync(
            PropositionBatch.Save(new StoredProposition("customer.derived", "customer", DerivedDocument, 1, null)),
            default);

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
    public void Should_reject_a_second_AddPropositions_call()
    {
        // Arrange — DI is last-wins, so a second call would silently discard the first store:
        // no double Load, no exception, an argument quietly ignored. MotivRulesOptions.AddModel
        // already throws on a duplicate id; this is the same contract one layer up.
        var (registry, options) = Fixture();
        var builder = new ServiceCollection().AddMotivRules(registry, options);
        builder.AddPropositions();

        // Act
        var second = () => builder.AddPropositions(new InMemoryPropositionStore());

        // Assert
        second.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public async Task Should_include_authored_propositions_in_the_catalog()
    {
        // Arrange — the regression guard for the catalog being a closed-over constant
        await using var app = await StartAsync(rules => rules.AddPropositions());
        var client = app.GetTestClient();
        await AuthorDerived(app.Services);

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
        await AuthorDerived(app.Services);

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
