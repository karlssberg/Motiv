namespace Motiv.Serialization.Tests.Rules;

public class RuleFlavourTests
{
    // Plain class (not a record) so the net472 target compiles without an IsExternalInit polyfill.
    private sealed class Customer(bool isActive)
    {
        public bool IsActive { get; } = isActive;
    }

    // Explicit property (no init accessor) so the net472 target compiles while keeping record equality.
    private sealed record Verdict(string Code)
    {
        public string Code { get; } = Code;
    }

    private static PolicyBase<Customer, string> IsActivePolicy { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static AsyncSpecBase<Customer, string> CreditCheck { get; } =
        Spec.BuildAsync((Customer c) => new ValueTask<bool>(c.IsActive))
            .WhenTrue("passes").WhenFalse("fails").Create();

    private static AsyncPolicyBase<Customer, string> CreditPolicy { get; } =
        Spec.BuildAsync((Customer c) => new ValueTask<bool>(c.IsActive))
            .WhenTrue("approved").WhenFalse("declined").Create();

    private static SpecRegistry Registry() => new SpecRegistry()
        .Register("is-active", IsActivePolicy)
        .Register("credit-check", CreditCheck);

    private sealed class ActivePolicyRule() : PolicyRule<Customer, string>("active-policy", IsActivePolicy);

    private sealed class CreditRule() : AsyncRule<Customer, string>("credit", CreditCheck);

    private sealed class CreditPolicyRule() : AsyncPolicyRule<Customer, string>("credit-policy", CreditPolicy);

    [Fact]
    public void Should_shadow_evaluate_with_the_policy_result()
    {
        // Arrange
        var rule = new ActivePolicyRule();
        new RuleSet(Registry()).Add(rule);

        // Act
        PolicyResultBase<string> result = rule.Evaluate(new Customer(true));

        // Assert
        result.Value.ShouldBe("active");
        rule.IsPolicy.ShouldBeTrue();
        rule.ShouldBeAssignableTo<Rule<Customer, string>>();   // usable as its spec-flavoured base
    }

    private sealed class VerdictRule() : PolicyRule<Customer, Verdict>(
        "verdict",
        Spec.Build((Customer c) => c.IsActive)
            .WhenTrue(new Verdict("OK")).WhenFalse(new Verdict("DENIED")).Create("is approved"));

    [Fact]
    public void Should_yield_typed_metadata_from_a_policy_rule()
    {
        // Arrange — the typed-metadata DX: TMetadata inline as a generic argument, no casts
        var rule = new VerdictRule();
        new RuleSet(Registry()).Add(rule);

        // Act
        Verdict verdict = rule.Evaluate(new Customer(true)).Value;

        // Assert
        verdict.ShouldBe(new Verdict("OK"));
        rule.MetadataType.ShouldBe(typeof(Verdict));
    }

    [Fact]
    public void Should_reject_document_updates_that_do_not_bind_to_a_policy()
    {
        // Arrange — a bare 'and' composition binds to a spec, not a policy
        var rule = new ActivePolicyRule();
        var rules = new RuleSet(Registry()).Add(rule);
        var nonPolicy = """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "is-active" } ] } }""";

        // Act
        var outcome = rules.Update("active-policy", nonPolicy, 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        outcome.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.PolicyRequired);
        rule.Version.ShouldBe(1);
    }

    [Fact]
    public void Should_accept_document_updates_that_bind_to_a_policy()
    {
        // Arrange — a leaf referencing a registered policy stays a policy at runtime
        var rule = new ActivePolicyRule();
        var rules = new RuleSet(Registry()).Add(rule);

        // Act
        var outcome = rules.Update("active-policy", """{ "rule": { "spec": "is-active" } }""", 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        rule.Evaluate(new Customer(false)).Value.ShouldBe("inactive");
    }

    private sealed class SpecDefaultPolicyRule() : PolicyRule<Customer, string>(
        "spec-default-policy",
        RuleDocuments.FromJson("""{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "is-active" } ] } }"""));

    [Fact]
    public void Should_fail_fast_at_add_when_a_policy_rules_default_document_binds_to_a_spec()
    {
        // Arrange — the document default binds to an 'and' composition, which is not a policy
        var rules = new RuleSet(Registry());

        // Act & Assert
        Should.Throw<RuleSerializationException>(() => rules.Add(new SpecDefaultPolicyRule()))
            .Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.PolicyRequired);
    }

    [Fact]
    public async Task Should_evaluate_async_rules_and_swap_them_atomically()
    {
        // Arrange
        var rule = new CreditRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rule.IsAsync.ShouldBeTrue();

        // Act — default, then a swapped mixed document
        var before = await rule.EvaluateAsync(new Customer(true));
        var outcome = rules.Update("credit",
            """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "credit-check" } ] } }""", 1);
        var after = await rule.EvaluateAsync(new Customer(true));

        // Assert
        before.Satisfied.ShouldBeTrue();
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        after.Satisfied.ShouldBeTrue();
        after.Assertions.ShouldBe(["active", "passes"]);
    }

    [Fact]
    public async Task Should_revert_async_rules_to_their_default_and_bump_the_version()
    {
        // Arrange
        var rule = new CreditRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rules.Update("credit",
            """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "credit-check" } ] } }""", 1)
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        // Act
        var outcome = rules.Revert("credit", expectedVersion: 2);
        var result = await rule.EvaluateAsync(new Customer(true));

        // Assert — revert is an update: version moves forward, never back
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        outcome.Version.ShouldBe(3);
        rule.DocumentJson.ShouldBeNull();
        result.Assertions.ShouldBe(["passes"]);
    }

    private sealed class RuleFlavourSyncProbe() : Rule<Customer, string>("sync-probe", IsActivePolicy);

    [Fact]
    public void Should_reject_sync_documents_with_async_leaves_on_sync_rules()
    {
        // Arrange — a sync Rule must not accept a document referencing an async spec
        var rule = new RuleFlavourSyncProbe();
        var rules = new RuleSet(Registry()).Add(rule);

        // Act
        var outcome = rules.Update("sync-probe", """{ "rule": { "spec": "credit-check" } }""", 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        outcome.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.AsyncSpecInSyncLoad);
    }

    [Fact]
    public async Task Should_shadow_evaluate_async_with_the_policy_result()
    {
        // Arrange
        var rule = new CreditPolicyRule();
        new RuleSet(Registry()).Add(rule);
        rule.IsPolicy.ShouldBeTrue();
        rule.IsAsync.ShouldBeTrue();

        // Act
        PolicyResultBase<string> result = await rule.EvaluateAsync(new Customer(true));

        // Assert
        result.Value.ShouldBe("approved");
        rule.ShouldBeAssignableTo<AsyncRule<Customer, string>>();   // usable as its spec-flavoured base
    }

    [Fact]
    public void Should_reject_async_document_updates_that_do_not_bind_to_a_policy()
    {
        // Arrange — a bare 'and' composition binds to an async spec, not an async policy
        var rule = new CreditPolicyRule();
        var rules = new RuleSet(Registry()).Add(rule);
        var nonPolicy = """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "credit-check" } ] } }""";

        // Act
        var outcome = rules.Update("credit-policy", nonPolicy, 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        outcome.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.PolicyRequired);
        rule.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Should_accept_async_document_updates_that_bind_to_a_policy()
    {
        // Arrange — a whenTrue/whenFalse-decorated root binds to an async policy
        var rule = new CreditPolicyRule();
        var rules = new RuleSet(Registry()).Add(rule);
        var policyShaped =
            """{ "rule": { "spec": "credit-check", "whenTrue": "credit ok", "whenFalse": "credit not ok" } }""";

        // Act
        var outcome = rules.Update("credit-policy", policyShaped, 1);
        var result = await rule.EvaluateAsync(new Customer(false));

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        result.Value.ShouldBe("credit not ok");
    }

    private sealed class SpecDefaultAsyncPolicyRule() : AsyncPolicyRule<Customer, string>(
        "spec-default-async-policy",
        RuleDocuments.FromJson("""{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "credit-check" } ] } }"""));

    [Fact]
    public void Should_fail_fast_at_add_when_an_async_policy_rules_default_document_binds_to_a_spec()
    {
        // Arrange — the document default binds to an 'and' composition, which is not an async policy
        var rules = new RuleSet(Registry());

        // Act & Assert
        Should.Throw<RuleSerializationException>(() => rules.Add(new SpecDefaultAsyncPolicyRule()))
            .Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.PolicyRequired);
    }

    [Fact]
    public async Task Should_revert_async_policy_rules_to_their_default_and_yield_the_policy_value()
    {
        // Arrange
        var rule = new CreditPolicyRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rules.Update("credit-policy",
            """{ "rule": { "spec": "credit-check", "whenTrue": "credit ok", "whenFalse": "credit not ok" } }""", 1)
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        // Act
        var outcome = rules.Revert("credit-policy", expectedVersion: 2);
        var result = await rule.EvaluateAsync(new Customer(true));

        // Assert — back on the compiled default, still yielding the typed policy value
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        outcome.Version.ShouldBe(3);
        rule.DocumentJson.ShouldBeNull();
        result.Value.ShouldBe("approved");
    }

    [Fact]
    public void Should_throw_when_an_async_rule_is_evaluated_before_being_added_to_a_rule_set()
    {
        // Arrange
        var rule = new CreditRule();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => rule.EvaluateAsync(new Customer(true)))
            .Message.ShouldContain("RuleSet");
    }

    [Fact]
    public void Should_report_a_version_conflict_for_a_stale_async_update()
    {
        // Arrange
        var rule = new CreditRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rules.Update("credit", """{ "rule": { "spec": "credit-check" } }""", 1)
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        // Act — second writer still believes version 1
        var outcome = rules.Update("credit", """{ "rule": { "spec": "credit-check" } }""", 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        outcome.Version.ShouldBe(2);
        rule.Version.ShouldBe(2);
    }

    private sealed class DocumentDefaultAsyncRule() : AsyncRule<Customer, string>(
        "doc-async",
        RuleDocuments.FromJson("""{ "rule": { "spec": "credit-check" } }"""));

    [Fact]
    public async Task Should_bind_an_async_document_default_at_add()
    {
        // Arrange
        var rule = new DocumentDefaultAsyncRule();

        // Act
        new RuleSet(Registry()).Add(rule);
        var result = await rule.EvaluateAsync(new Customer(true));

        // Assert
        rule.Version.ShouldBe(1);
        rule.DocumentJson.ShouldNotBeNull();
        result.Assertions.ShouldBe(["passes"]);
    }
}
