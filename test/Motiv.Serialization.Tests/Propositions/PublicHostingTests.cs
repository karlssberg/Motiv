using System.Reflection;
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

/// <summary>
/// The proposition surface as a package consumer sees it. Everything here is arranged through
/// public constructors only — no <c>BindingScope</c>, no <c>InternalsVisibleTo</c> — so the file
/// doubles as the executable statement of what actually ships. The test project *can* see internals,
/// which is exactly why <see cref="Should_expose_the_whole_hosting_path_publicly"/> exists: it fails
/// if any step below stops being reachable from outside the assembly.
/// </summary>
public class PublicHostingTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private sealed class CanCheckoutRule() : Rule<Customer, string>("can-checkout", IsActive);

    private sealed class AuthoredDefaultRule()
        : Rule<Customer, string>(
            "can-checkout-authored",
            RuleDocuments.FromJson("""{ "rule": { "spec": "customer.eligible" } }"""));

    private static SpecRegistry NewRegistry() =>
        new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult);

    /// <summary>
    /// The opening move of the supported arrangement, spelled the way a consumer would: a fresh
    /// registry, a store, then the model the authored documents are written against.
    /// </summary>
    private static PropositionSet NewPropositions(IPropositionStore? store = null) =>
        new PropositionSet(NewRegistry(), store ?? new InMemoryPropositionStore())
            .AddModel<Customer>("customer");

    /// <summary>
    /// The whole point of pairing the two sets: an edit to a proposition has to reach the rules that
    /// reference it. Constructed entirely through the public API, with the rule never touched again
    /// after its document is set.
    /// </summary>
    [Fact]
    public async Task Should_cascade_a_proposition_edit_into_a_rule_over_the_public_api()
    {
        // Arrange — the supported public path: propositions first, then rules built from them
        var propositions = NewPropositions();
        propositions.Load();

        var rule = new CanCheckoutRule();
        var rules = new RuleSet(propositions).Add(rule);

        (await propositions.CreateAsync("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        (await rules.UpdateAsync(
            "can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1, new RuleChangeProvenance("test")))
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        var inactiveAdult = new Customer(IsActive: false, Age: 30);
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeFalse();

        // Act — only the proposition moves
        (await propositions.UpdateAsync("customer.eligible", """{ "rule": { "spec": "customer.is-adult" } }""", 1))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Updated);

        // Assert — the rule's evaluation follows
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// The sharing story stated directly: a rule set obtained from a proposition set is visible to
    /// that proposition set's dependency graph, which is what makes the cascade and the
    /// prepare-all-then-commit-all transaction span both.
    /// </summary>
    [Fact]
    public async Task Should_see_a_publicly_built_rule_set_as_a_dependent()
    {
        // Arrange
        var propositions = NewPropositions();
        var rules = new RuleSet(propositions).Add(new CanCheckoutRule());
        await propositions.CreateAsync("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await rules.UpdateAsync(
            "can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1, new RuleChangeProvenance("test"));

        // Act
        var dependents = propositions.Dependents("customer.eligible");

        // Assert — one scope, so the rule is in the closure
        dependents.Count.ShouldBe(1);
        dependents[0].Name.ShouldBe("can-checkout");
        dependents[0].Kind.ShouldBe("rule");
        (await propositions.WithdrawAsync("customer.eligible", 1)).Outcome.ShouldBe(PropositionUpdateOutcome.Referenced);
    }

    /// <summary>
    /// A rule registered with a document default references propositions the moment it is added, so
    /// the public path has to let propositions be authored and loaded before the rule set exists.
    /// </summary>
    [Fact]
    public async Task Should_bind_a_rule_default_against_a_proposition_loaded_from_the_store()
    {
        // Arrange — a store already holding a proposition, as it would be after a restart
        var store = new InMemoryPropositionStore();
        await store.WriteAsync(
            PropositionBatch.Save(new StoredProposition(
                "customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", 1, null)),
            default);

        var propositions = NewPropositions(store);
        propositions.Load();

        // Act — the rule's *default* resolves the loaded proposition
        var rule = new AuthoredDefaultRule();
        new RuleSet(propositions).Add(rule);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeFalse();

        // Assert — and the cascade still reaches it
        (await propositions.UpdateAsync("customer.eligible", """{ "rule": { "spec": "customer.is-adult" } }""", 1))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// The trap this design exists to close. Two sets built from the same registry get two binding
    /// scopes, so the cascade quietly does nothing — a failure that looks like success. The registry
    /// remembers who opened a scope over it, so the second construction is refused rather than
    /// silently mis-wired.
    /// </summary>
    [Fact]
    public void Should_refuse_a_rule_set_built_from_a_registry_a_proposition_set_already_owns()
    {
        // Arrange
        var registry = NewRegistry();
        _ = new PropositionSet(registry, new InMemoryPropositionStore());

        // Act
        var act = () => new RuleSet(registry);

        // Assert
        var exception = act.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("new RuleSet(propositionSet)");
    }

    /// <summary>The same trap approached from the other side, where the rule set is built first.</summary>
    [Fact]
    public void Should_refuse_a_proposition_set_built_from_a_registry_a_rule_set_already_owns()
    {
        // Arrange
        var registry = NewRegistry();
        _ = new RuleSet(registry);

        // Act
        var act = () => new PropositionSet(registry, new InMemoryPropositionStore());

        // Assert
        var exception = act.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("new RuleSet(propositionSet)");
    }

    /// <summary>Two rule sets over one registry never shared a scope, and are none of this design's business.</summary>
    [Fact]
    public void Should_still_allow_two_rule_sets_over_one_registry()
    {
        // Arrange
        var registry = NewRegistry();

        // Act
        var act = () =>
        {
            _ = new RuleSet(registry);
            _ = new RuleSet(registry);
        };

        // Assert
        act.ShouldNotThrow();
    }

    /// <summary>Building a rule set from a proposition set is the supported pairing, not a second claim.</summary>
    [Fact]
    public void Should_allow_several_rule_sets_paired_with_one_proposition_set()
    {
        // Arrange
        var propositions = NewPropositions();

        // Act
        var act = () =>
        {
            _ = new RuleSet(propositions);
            _ = new RuleSet(propositions);
        };

        // Assert
        act.ShouldNotThrow();
    }

    [Fact]
    public void Should_reject_a_null_registry()
    {
        // Arrange
        // The cast is an artefact of this assembly seeing the internal BindingScope overload; a
        // consumer has only one constructor to bind to here.
        var act = () => new PropositionSet((SpecRegistry)null!, new InMemoryPropositionStore());

        // Act / Assert
        act.ShouldThrow<ArgumentNullException>().ParamName!.ShouldBe("registry");
    }

    [Fact]
    public void Should_reject_a_null_store()
    {
        // Arrange
        var act = () => new PropositionSet(NewRegistry(), null!);

        // Act / Assert
        act.ShouldThrow<ArgumentNullException>().ParamName!.ShouldBe("store");
    }

    /// <summary>
    /// The claim is a mutation of the caller's registry, so a constructor that throws must not leave
    /// one behind — otherwise a rejected argument would poison a registry that is still perfectly
    /// usable, refusing a later rule set on behalf of a proposition set that was never built.
    /// </summary>
    [Fact]
    public void Should_leave_a_registry_unclaimed_when_construction_throws()
    {
        // Arrange
        var registry = NewRegistry();
        Should.Throw<ArgumentNullException>(() => new PropositionSet(registry, null!));

        // Act
        var act = () => new RuleSet(registry);

        // Assert
        act.ShouldNotThrow();
    }

    [Fact]
    public void Should_reject_a_null_proposition_set()
    {
        // Arrange
        var act = () => new RuleSet((PropositionSet)null!);

        // Act / Assert
        act.ShouldThrow<ArgumentNullException>().ParamName!.ShouldBe("propositions");
    }

    /// <summary>
    /// The guarantee the rest of this file cannot make on its own: this assembly can see
    /// <c>Motiv.Serialization</c>'s internals, so a hosting path that had drifted back behind an
    /// internal type would still compile here while being unreachable to an actual consumer. Assert
    /// the seam's visibility directly instead.
    /// </summary>
    [Fact]
    public void Should_expose_the_whole_hosting_path_publicly()
    {
        // Act
        var propositionSetConstructor = typeof(PropositionSet).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance, null,
            [typeof(SpecRegistry), typeof(IPropositionStore), typeof(RuleSerializerOptions)], null);
        var ruleSetConstructor = typeof(RuleSet).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance, null,
            [typeof(PropositionSet), typeof(IRuleStore), typeof(RuleSerializerOptions), typeof(DecisionLog)],
            null);

        // Assert
        propositionSetConstructor.ShouldNotBeNull();
        ruleSetConstructor.ShouldNotBeNull();
        // A public constructor over an internal parameter type would still be unreachable outside.
        propositionSetConstructor.GetParameters().ShouldAllBe(parameter => parameter.ParameterType.IsPublic);
        ruleSetConstructor.GetParameters().ShouldAllBe(parameter => parameter.ParameterType.IsPublic);
        typeof(PropositionSet).GetMethod(nameof(PropositionSet.AddModel))!.IsPublic.ShouldBeTrue();
        typeof(PropositionSet).GetMethod(nameof(PropositionSet.Load))!.IsPublic.ShouldBeTrue();
    }

    private sealed record Customer(bool IsActive, int Age);
}
