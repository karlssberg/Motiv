namespace Motiv.Serialization.Tests.Diagnostics;

/// <summary>
/// The proposition half of the authoring instruments. The two stores are never written in the same
/// transaction, so they are counted apart, under the same instruments and a different
/// <c>motiv.rules.kind</c>.
/// </summary>
[Collection(RulesTelemetryTestCollection.Name)]
public class PropositionTelemetryTests
{
    private sealed class Customer(bool isActive)
    {
        public bool IsActive { get; } = isActive;
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private const string ActiveDocument = """{"rule":{"spec":"customer.is-active"}}""";
    private const string UnknownSpec = """{"rule":{"spec":"no-such-spec"}}""";

    private static (PropositionSet Set, InMemoryPropositionStore Store) NewSet(
        InMemoryPropositionStore? store = null)
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var scope = new BindingScope(registry);
        return (new PropositionSet(scope, store ??= new InMemoryPropositionStore())
            .AddModel<Customer>("customer"), store);
    }

    [Fact]
    public async Task Should_count_a_document_that_would_not_bind_as_a_publish_phase_bind_failure()
    {
        // Arrange
        var (set, _) = NewSet();
        using var harness = new RulesTelemetryHarness();

        // Act
        var result = await set.CreateAsync("customer.broken", "customer", UnknownSpec, description: null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        var failure = harness.Single("motiv.rules.bind_failures");
        failure.Value.ShouldBe(1);
        failure.Tag("motiv.rules.kind").ShouldBe("proposition");
        failure.Tag("motiv.rules.phase").ShouldBe("publish");
    }

    [Fact]
    public async Task Should_count_a_stale_expected_version_as_a_publish_conflict()
    {
        // Arrange
        var (set, _) = NewSet();
        await set.CreateAsync("customer.mine", "customer", ActiveDocument, description: null);
        using var harness = new RulesTelemetryHarness();

        // Act — version 1 is what it was created at, so naming 0 is the stale read.
        var result = await set.UpdateAsync("customer.mine", ActiveDocument, expectedVersion: 0);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.VersionConflict);
        var conflict = harness.Single("motiv.rules.publish_conflicts");
        conflict.Value.ShouldBe(1);
        conflict.Tag("motiv.rules.kind").ShouldBe("proposition");
    }

    [Fact]
    public async Task Should_time_the_store_calls_a_publish_makes()
    {
        // Arrange
        var (set, _) = NewSet();
        using var harness = new RulesTelemetryHarness();

        // Act
        await set.CreateAsync("customer.mine", "customer", ActiveDocument, description: null);

        // Assert
        var timings = harness.For("motiv.rules.store.duration")
            .Where(measurement => measurement.Tag("motiv.rules.kind")!.Equals("proposition"));
        timings.Select(measurement => measurement.Tag("motiv.rules.operation")).Distinct()
            .ShouldBe(["generation", "append"], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_count_a_row_quarantined_at_load_as_a_load_phase_bind_failure()
    {
        // Arrange — a build that knows the spec publishes; a build that does not then loads the row.
        var store = new InMemoryPropositionStore();
        var (publisher, _) = NewSet(store);
        await publisher.CreateAsync("customer.mine", "customer", ActiveDocument, description: null);

        var stranger = new PropositionSet(new BindingScope(new SpecRegistry()), store)
            .AddModel<Customer>("customer");

        using var harness = new RulesTelemetryHarness();

        // Act
        stranger.Load();

        // Assert
        var failure = harness.For("motiv.rules.bind_failures")
            .Single(measurement => measurement.Tag("motiv.rules.phase")!.Equals("load"));
        failure.Value.ShouldBe(1);
        failure.Tag("motiv.rules.kind").ShouldBe("proposition");
    }

    [Fact]
    public async Task Should_report_the_proposition_half_of_the_catalog()
    {
        // Arrange
        var (set, _) = NewSet();
        await set.CreateAsync("customer.mine", "customer", ActiveDocument, description: null);

        using var harness = new RulesTelemetryHarness();

        // Act
        harness.Collect();

        // Assert
        harness.For("motiv.rules.catalog.size")
            .ShouldContain(m => m.Tag("motiv.rules.kind")!.Equals("proposition") && m.Value == 1);
    }
}
