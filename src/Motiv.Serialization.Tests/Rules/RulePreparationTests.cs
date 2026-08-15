namespace Motiv.Serialization.Tests.Rules;

public class RulePreparationTests
{
    // Plain class (not a record) so the net472 target compiles without an IsExternalInit polyfill.
    private sealed class Customer(bool isActive)
    {
        public bool IsActive { get; } = isActive;
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class SampleRule() : Rule<Customer, string>("sample", IsActive);

    private const string Document = """{ "rule": { "spec": "customer.is-active" } }""";

    private static (RuleSet Set, SampleRule Rule) Bound()
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var rule = new SampleRule();
        return (new RuleSet(registry).Add(rule), rule);
    }

    [Fact]
    public void Should_not_move_the_live_rule_when_only_prepared()
    {
        // Arrange
        var (set, rule) = Bound();

        // Act — prepare, but never commit
        var prepared = set.PrepareUpdateCore("sample", Document, expectedVersion: 1);

        // Assert — the whole point of the split: nothing is live until Commit runs
        prepared.Publication.ShouldNotBeNull();
        prepared.Publication!.Version.ShouldBe(2);
        rule.Version.ShouldBe(1);
        rule.DocumentJson.ShouldBeNull();
    }

    [Fact]
    public void Should_publish_only_once_committed()
    {
        // Arrange
        var (set, rule) = Bound();
        var prepared = set.PrepareUpdateCore("sample", Document, expectedVersion: 1);

        // Act
        prepared.Publication!.ApplyTo(new ScopeGenerationBuilder(set.Scope.Registry, set.Scope.Current));

        // Assert
        rule.Version.ShouldBe(2);
        rule.DocumentJson!.ShouldBe(Document);
    }

    [Fact]
    public void Should_refuse_a_stale_expected_version_before_binding()
    {
        // Arrange
        var (set, _) = Bound();

        // Act
        var prepared = set.PrepareUpdateCore("sample", Document, expectedVersion: 99);

        // Assert
        prepared.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        prepared.Version.ShouldBe(1);
        prepared.Publication.ShouldBeNull();
    }

    [Fact]
    public void Should_report_an_unbindable_document_as_invalid()
    {
        // Arrange
        var (set, _) = Bound();

        // Act
        var prepared = set.PrepareUpdateCore(
            "sample", """{ "rule": { "spec": "customer.does-not-exist" } }""", expectedVersion: 1);

        // Assert
        prepared.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        prepared.Errors.ShouldNotBeEmpty();
        prepared.Publication.ShouldBeNull();
    }

    [Fact]
    public void Should_report_an_unknown_rule_as_not_found()
    {
        // Arrange
        var (set, _) = Bound();

        // Act
        var prepared = set.PrepareUpdateCore("nope", Document, expectedVersion: 1);

        // Assert
        prepared.Outcome.ShouldBe(RuleUpdateOutcome.NotFound);
        prepared.Publication.ShouldBeNull();
    }

    [Fact]
    public void Should_prepare_a_revert_carrying_the_defaults_document()
    {
        // Arrange
        var (set, rule) = Bound();
        set.PrepareUpdateCore("sample", Document, expectedVersion: 1).Publication!
            .ApplyTo(new ScopeGenerationBuilder(set.Scope.Registry, set.Scope.Current));

        // Act
        var prepared = set.PrepareRevertCore("sample", expectedVersion: 2);

        // Assert — a compiled default publishes a null document, and the version still moves forward
        prepared.Publication.ShouldNotBeNull();
        prepared.Publication!.Version.ShouldBe(3);
        prepared.Publication.DocumentJson.ShouldBeNull();
        rule.Version.ShouldBe(2);
    }

    [Fact]
    public void Should_refuse_to_report_a_failure_result_for_a_successful_prepare()
    {
        // Arrange
        var (set, _) = Bound();
        var prepared = set.PrepareUpdateCore("sample", Document, expectedVersion: 1);

        // Act / Assert — reporting a publish that has not happened is the bug this guards
        Should.Throw<InvalidOperationException>(() => prepared.ToFailureResult());
    }
}
