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

    private static AsyncSpecBase<Customer, string> PassesAdultCheck { get; } =
        Spec.BuildAsync(async (Customer c) => { await Task.Yield(); return c.Age >= 18; })
            .WhenTrue("adult-check-passes").WhenFalse("adult-check-fails").Create();

    private static PolicyBase<Customer, string> IsActivePolicy { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    // Composition returns a Spec, not a Policy (CLAUDE.md's "Policy Preservation" rule), so this
    // is a non-policy a PolicyRule must refuse to rebind to.
    private static SpecBase<Customer, string> ComposedNonPolicy { get; } = IsActive & IsAdult;

    private static AsyncPolicyBase<Customer, string> PassesCheckPolicy { get; } =
        Spec.BuildAsync(async (Customer c) => { await Task.Yield(); return c.IsActive; })
            .WhenTrue("passes").WhenFalse("fails").Create();

    private static AsyncSpecBase<Customer, string> ComposedAsyncNonPolicy { get; } = PassesCheck & PassesAdultCheck;

    private sealed class CanCheckoutRule() : Rule<Customer, string>("can-checkout", IsActive);

    private sealed class CanCheckoutAsyncRule() : AsyncRule<Customer, string>("can-checkout-async", PassesCheck);

    private sealed class CanCheckoutPolicyRule() : PolicyRule<Customer, string>("can-checkout-policy", IsActivePolicy);

    private sealed class CanCheckoutAsyncPolicyRule()
        : AsyncPolicyRule<Customer, string>("can-checkout-async-policy", PassesCheckPolicy);

    private sealed class AuthoredDefaultRule()
        : Rule<Customer, string>(
            "can-checkout-authored",
            RuleDocuments.FromJson("""{ "rule": { "spec": "customer.eligible" } }"""));

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
    public async Task Should_rebind_a_rule_when_a_proposition_it_references_changes()
    {
        // Arrange — the feature's central claim, now across the rule boundary
        var (propositions, rules, rule) = NewHost();
        await propositions.CreateAsync("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await rules.UpdateAsync(
            "can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1, new RuleChangeProvenance("test"));
        var inactiveAdult = new Customer(IsActive: false, Age: 30);
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeFalse();

        // Act — the rule is never touched again
        await propositions.UpdateAsync("customer.eligible", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// A rule registered with a *document* default already references propositions the moment it is
    /// added, so registration — not just <see cref="RuleSet.UpdateAsync"/> — has to record its edges.
    /// Every other cascade test starts from a compiled default, which references nothing.
    /// </summary>
    [Fact]
    public async Task Should_track_a_rule_whose_default_document_references_a_proposition()
    {
        // Arrange — the proposition has to exist before the rule's default can bind against it
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        await propositions.CreateAsync("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        var rule = new AuthoredDefaultRule();
        new RuleSet(scope).Add(rule);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeFalse();

        // Act — the rule is never updated; only the proposition beneath it moves
        (await propositions.UpdateAsync("customer.eligible", """{ "rule": { "spec": "customer.is-adult" } }""", 1))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Updated);

        // Assert
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_rebind_an_async_rule_when_a_proposition_it_references_changes()
    {
        // Arrange — the cascade must reach async rules too, not just sync ones
        var registry = new SpecRegistry()
            .Register("customer.passes-check", PassesCheck)
            .Register("customer.passes-adult-check", PassesAdultCheck);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        var rule = new CanCheckoutAsyncRule();
        var rules = new RuleSet(scope).Add(rule);

        await propositions.CreateAsync("customer.eligible-async", "customer", """{ "rule": { "spec": "customer.passes-check" } }""", null);
        await rules.UpdateAsync(
            "can-checkout-async", """{ "rule": { "spec": "customer.eligible-async" } }""", 1,
            new RuleChangeProvenance("test"));
        var inactiveAdult = new Customer(IsActive: false, Age: 30);
        (await rule.EvaluateAsync(inactiveAdult)).Satisfied.ShouldBeFalse();

        // Act — the rule is never touched again
        await propositions.UpdateAsync("customer.eligible-async", """{ "rule": { "spec": "customer.passes-adult-check" } }""", 1);

        // Assert
        (await rule.EvaluateAsync(inactiveAdult)).Satisfied.ShouldBeTrue();
    }

    /// <summary>
    /// <see cref="PolicyRule{TModel,TMetadata}.Evaluate"/> does an unchecked cast to
    /// <c>PolicyBase&lt;TModel,TMetadata&gt;</c>. If a cascade ever let a policy rule bind to a
    /// plain spec, every later evaluation of the live rule would throw
    /// <see cref="InvalidCastException"/> instead of the cascade refusing the edit up front — so
    /// this guards the check that stands between a bad rebind and a crash on the hot path.
    /// </summary>
    [Fact]
    public async Task Should_reject_a_cascade_rebind_that_would_turn_a_policy_rule_into_a_spec()
    {
        // Arrange
        var registry = new SpecRegistry()
            .Register("customer.is-active-policy", IsActivePolicy)
            .Register("customer.composed", ComposedNonPolicy);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        var rule = new CanCheckoutPolicyRule();
        var rules = new RuleSet(scope).Add(rule);

        await propositions.CreateAsync("customer.eligible-policy", "customer", """{ "rule": { "spec": "customer.is-active-policy" } }""", null);
        (await rules.UpdateAsync(
            "can-checkout-policy", """{ "rule": { "spec": "customer.eligible-policy" } }""", 1,
            new RuleChangeProvenance("test")))
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        // Act — eligible-policy now resolves to a non-policy spec; can-checkout-policy requires a policy
        var result = await propositions.UpdateAsync("customer.eligible-policy", """{ "rule": { "spec": "customer.composed" } }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.BrokenDependents.Count.ShouldBe(1);
        result.BrokenDependents[0].Name.ShouldBe("can-checkout-policy");
        result.BrokenDependents[0].Errors.ShouldContain(error => error.Code == RuleErrorCode.PolicyRequired);

        // The rule itself is untouched, and still bound to a policy
        rule.Version.ShouldBe(2);
        rule.Evaluate(new Customer(IsActive: true, Age: 30)).Satisfied.ShouldBeTrue();
    }

    /// <summary>The async counterpart: <see cref="AsyncPolicyRule{TModel,TMetadata}.EvaluateAsync"/> carries the same unchecked cast.</summary>
    [Fact]
    public async Task Should_reject_a_cascade_rebind_that_would_turn_an_async_policy_rule_into_a_spec()
    {
        // Arrange
        var registry = new SpecRegistry()
            .Register("customer.passes-check-policy", PassesCheckPolicy)
            .Register("customer.composed-async", ComposedAsyncNonPolicy);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        var rule = new CanCheckoutAsyncPolicyRule();
        var rules = new RuleSet(scope).Add(rule);

        await propositions.CreateAsync("customer.eligible-async-policy", "customer", """{ "rule": { "spec": "customer.passes-check-policy" } }""", null);
        (await rules.UpdateAsync(
            "can-checkout-async-policy", """{ "rule": { "spec": "customer.eligible-async-policy" } }""", 1,
            new RuleChangeProvenance("test")))
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        // Act — eligible-async-policy now resolves to a non-policy async spec
        var result = await propositions.UpdateAsync("customer.eligible-async-policy", """{ "rule": { "spec": "customer.composed-async" } }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.BrokenDependents.Count.ShouldBe(1);
        result.BrokenDependents[0].Name.ShouldBe("can-checkout-async-policy");
        result.BrokenDependents[0].Errors.ShouldContain(error => error.Code == RuleErrorCode.PolicyRequired);

        // The rule itself is untouched, and still bound to a policy — the unchecked cast inside
        // EvaluateAsync is precisely what a bad rebind would have turned into a crash, so actually
        // evaluating is the assertion this test exists to make
        rule.Version.ShouldBe(2);
        (await rule.EvaluateAsync(new Customer(IsActive: true, Age: 30))).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_leave_a_rebound_rules_version_alone()
    {
        // Arrange
        var (propositions, rules, rule) = NewHost();
        await propositions.CreateAsync("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await rules.UpdateAsync(
            "can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1, new RuleChangeProvenance("test"));
        var versionBefore = rule.Version;

        // Act
        await propositions.UpdateAsync("customer.eligible", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert — its document did not change, so neither does its version
        rule.Version.ShouldBe(versionBefore);
    }

    /// <summary>
    /// The concrete way a *valid* edit breaks a dependent, and the failure the whole transactional
    /// design exists to catch: a sync rule cannot bind a proposition that has just become async.
    /// </summary>
    [Fact]
    public async Task Should_reject_a_proposition_edit_that_makes_a_sync_rule_unbindable()
    {
        // Arrange
        var (propositions, rules, rule) = NewHost();
        await propositions.CreateAsync("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await rules.UpdateAsync(
            "can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1, new RuleChangeProvenance("test"));

        // Act — the new definition is perfectly valid on its own, but async
        var result = await propositions.UpdateAsync(
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
    public async Task Should_leave_the_proposition_and_the_rule_untouched_when_the_rule_would_break()
    {
        // Arrange
        var (propositions, rules, rule) = NewHost();
        await propositions.CreateAsync("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await rules.UpdateAsync(
            "can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1, new RuleChangeProvenance("test"));
        var inactiveAdult = new Customer(IsActive: false, Age: 30);

        // Act
        await propositions.UpdateAsync("customer.eligible", """{ "rule": { "spec": "customer.passes-check" } }""", 1);

        // Assert
        propositions.Find("customer.eligible")!.Version.ShouldBe(1);
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeFalse();
    }

    /// <summary>
    /// The UI's Override button POSTs against a name that exists only as a compiled spec, so this
    /// is a create — and unlike every other create, it lands on a name live rules already reference
    /// by that same name. Overriding therefore has to rebind them, exactly as updating does. Every
    /// other cascade test here starts from a name the rule could only have learned after the
    /// proposition existed, which is why this one is not a duplicate of them.
    /// </summary>
    [Fact]
    public async Task Should_rebind_a_rule_when_a_create_overrides_the_compiled_spec_it_references()
    {
        // Arrange — the rule references the *compiled* spec, by the name about to be overridden
        var (propositions, rules, rule) = NewHost();
        (await rules.UpdateAsync(
            "can-checkout", """{ "rule": { "spec": "customer.is-active" } }""", 1, new RuleChangeProvenance("test")))
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        var activeMinor = new Customer(IsActive: true, Age: 10);
        rule.Evaluate(activeMinor).Satisfied.ShouldBeTrue();

        // Act — the rule is never touched again
        (await propositions.CreateAsync("customer.is-active", "customer", """{ "rule": { "spec": "customer.is-adult" } }""", null))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        // Assert
        rule.Evaluate(activeMinor).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_list_a_rule_as_a_dependent_of_the_proposition_it_references()
    {
        // Arrange
        var (propositions, rules, _) = NewHost();
        await propositions.CreateAsync("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await rules.UpdateAsync(
            "can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1, new RuleChangeProvenance("test"));

        // Act
        var dependents = propositions.Dependents("customer.eligible");

        // Assert
        dependents.Count.ShouldBe(1);
        dependents[0].Name.ShouldBe("can-checkout");
        dependents[0].Kind.ShouldBe("rule");
    }

    [Fact]
    public async Task Should_refuse_to_remove_a_proposition_a_rule_references()
    {
        // Arrange
        var (propositions, rules, _) = NewHost();
        await propositions.CreateAsync("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await rules.UpdateAsync(
            "can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1, new RuleChangeProvenance("test"));

        // Act
        var result = await propositions.WithdrawAsync("customer.eligible", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Referenced);
        result.Referrers.ShouldBe(["can-checkout"]);
    }

    [Fact]
    public async Task Should_stop_tracking_a_rule_reverted_to_its_compiled_default()
    {
        // Arrange — a compiled default references nothing, so the rule leaves the graph
        var (propositions, rules, _) = NewHost();
        await propositions.CreateAsync("customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await rules.UpdateAsync(
            "can-checkout", """{ "rule": { "spec": "customer.eligible" } }""", 1, new RuleChangeProvenance("test"));

        // Act
        await rules.RevertAsync("can-checkout", 2, new RuleChangeProvenance("test"));

        // Assert
        propositions.Dependents("customer.eligible").ShouldBeEmpty();
        (await propositions.WithdrawAsync("customer.eligible", 1)).Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
    }

    // Built by hand rather than via DateTimeOffset.UnixEpoch — that static field is unavailable on
    // net472/netstandard2.0, two of this project's target frameworks.
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A cascade must not clear a quarantine. A quarantine means "the stored document would not bind,
    /// so I am running the compiled default". A cascade rebinds whatever document the rule currently
    /// carries — for a quarantined rule that is the default, never the stored document — so the rebind
    /// succeeding says nothing about whether the stored document would now bind. Clearing it would
    /// report a broken rule as healthy while it quietly kept running the default, and an operator
    /// would never learn a repair was still owed.
    /// </summary>
    [Fact]
    public async Task Should_keep_a_quarantine_when_a_cascade_rebinds_the_default_beneath_it()
    {
        // Arrange — a document-default rule referencing an authored proposition, over a stored
        // document that no longer binds. Load quarantines it and, having published nothing, never
        // re-tracks it, so the rule stays enrolled as a cascade participant on its default's edges.
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        await propositions.CreateAsync(
            "customer.eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        var store = new InMemoryRuleStore();
        (await store.AppendAsync(
            [
                new StoredRuleVersion(
                    "can-checkout-authored", 4, """{ "rule": { "spec": "customer.was-renamed-away" } }""",
                    "alice", Epoch, null, null, "test")
            ],
            default)).IsConflict.ShouldBeFalse();

        var rules = new RuleSet(scope, store).Add(new AuthoredDefaultRule());
        rules.Load().Quarantined.ShouldHaveSingleItem();
        rules.FindEntry("can-checkout-authored")!.Quarantine.ShouldNotBeEmpty();

        // Act — the proposition beneath the *default* is republished, cascading a rebind into the rule
        (await propositions.UpdateAsync(
                "customer.eligible", """{ "rule": { "spec": "customer.is-adult" } }""", 1))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Updated);

        // Assert — the rebind rebound the default; the stored document is as broken as it ever was
        var entry = rules.FindEntry("can-checkout-authored")!;
        entry.Quarantine.ShouldNotBeEmpty();

        // And the version the repair must be addressed against is still the one the store holds.
        entry.Version.ShouldBe(4);
    }

    [Fact]
    public async Task Should_keep_working_when_constructed_without_a_proposition_set()
    {
        // Arrange — the public constructor must stay usable for hosts that never author propositions
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var rule = new CanCheckoutRule();

        // Act
        var rules = new RuleSet(registry).Add(rule);

        // Assert
        (await rules.UpdateAsync(
            "can-checkout", """{ "rule": { "spec": "customer.is-active" } }""", 1, new RuleChangeProvenance("test")))
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        rule.Evaluate(new Customer(IsActive: true, Age: 30)).Satisfied.ShouldBeTrue();
    }

    private sealed record Customer(bool IsActive, int Age);
}
