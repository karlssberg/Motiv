using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class RuleCascadeTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private static AsyncSpecBase<Customer, string> PassesCheck { get; } =
        Spec.BuildAsync(async (Customer c) => { await Task.Yield(); return c.IsActive; })
            .WhenTrue("passes").WhenFalse("fails").Create();

    private sealed class CanCheckoutRule() : Rule<Customer, string>("can-checkout", IsActive);

    private static (PropositionSet Propositions, RuleSet Rules, CanCheckoutRule Rule) NewHost()
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult)
            .Register("customer.passes-check", PassesCheck);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        var rule = new CanCheckoutRule();
        var rules = new RuleSet(scope).Add(rule);
        return (propositions, rules, rule);
    }

    [Fact]
    public void Should_rebind_a_rule_when_a_proposition_it_references_changes()
    {
        // Arrange — the feature's central claim, now across the rule boundary
        var (propositions, rules, rule) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        rules.Update("can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeFalse();

        // Act — the rule is never touched again
        propositions.Update("customer.eligible", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_leave_a_rebound_rules_version_alone()
    {
        // Arrange
        var (propositions, rules, rule) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        rules.Update("can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1);
        var versionBefore = rule.Version;

        // Act
        propositions.Update("customer.eligible", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert — its document did not change, so neither does its version
        rule.Version.ShouldBe(versionBefore);
    }

    /// <summary>
    /// The concrete way a *valid* edit breaks a dependent, and the failure the whole transactional
    /// design exists to catch: a sync rule cannot bind a proposition that has just become async.
    /// </summary>
    [Fact]
    public void Should_reject_a_proposition_edit_that_makes_a_sync_rule_unbindable()
    {
        // Arrange
        var (propositions, rules, rule) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        rules.Update("can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1);

        // Act — the new definition is perfectly valid on its own, but async
        var result = propositions.Update(
            "customer.eligible", """{ "rule": { "spec": "customer.passes-check" } }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.BrokenDependents.Count.ShouldBe(1);
        result.BrokenDependents[0].Name.ShouldBe("can-checkout");
        result.BrokenDependents[0].Kind.ShouldBe("rule");
        result.BrokenDependents[0].Errors
            .ShouldContain(error => error.Code == RuleErrorCode.AsyncSpecInSyncLoad);
    }

    [Fact]
    public void Should_leave_the_proposition_and_the_rule_untouched_when_the_rule_would_break()
    {
        // Arrange
        var (propositions, rules, rule) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        rules.Update("can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);

        // Act
        propositions.Update("customer.eligible", """{ "rule": { "spec": "customer.passes-check" } }""", 1);

        // Assert
        propositions.Find("customer.eligible")!.Version.ShouldBe(1);
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_list_a_rule_as_a_dependent_of_the_proposition_it_references()
    {
        // Arrange
        var (propositions, rules, _) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        rules.Update("can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1);

        // Act
        var dependents = propositions.Dependents("customer.eligible");

        // Assert
        dependents.Count.ShouldBe(1);
        dependents[0].Name.ShouldBe("can-checkout");
        dependents[0].Kind.ShouldBe("rule");
    }

    [Fact]
    public void Should_refuse_to_remove_a_proposition_a_rule_references()
    {
        // Arrange
        var (propositions, rules, _) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        rules.Update("can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1);

        // Act
        var result = propositions.Withdraw("customer.eligible", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Referenced);
        result.Referrers.ShouldBe(["can-checkout"]);
    }

    [Fact]
    public void Should_stop_tracking_a_rule_reverted_to_its_compiled_default()
    {
        // Arrange — a compiled default references nothing, so the rule leaves the graph
        var (propositions, rules, _) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        rules.Update("can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1);

        // Act
        rules.Revert("can-checkout", 2);

        // Assert
        propositions.Dependents("customer.eligible").ShouldBeEmpty();
        propositions.Withdraw("customer.eligible", 1).Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
    }

    [Fact]
    public void Should_keep_working_when_constructed_without_a_proposition_set()
    {
        // Arrange — the public constructor must stay usable for hosts that never author propositions
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var rule = new CanCheckoutRule();

        // Act
        var rules = new RuleSet(registry).Add(rule);

        // Assert
        rules.Update("can-checkout", """{ "rule": { "spec": "customer.is-active" } }""", 1)
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        rule.Evaluate(new Customer(IsActive: true, Age: 30)).Satisfied.ShouldBeTrue();
    }

    private sealed record Customer(bool IsActive, int Age);
}
