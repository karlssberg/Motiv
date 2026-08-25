namespace Motiv.Serialization.Tests.Diagnostics;

/// <summary>
/// What an operator watching the authoring path sees: documents that would not bind, publishes
/// refused because the head had moved, and how long the store took to answer.
/// </summary>
[Collection(RulesTelemetryTestCollection.Name)]
public class StoreTelemetryTests
{
    private static SpecBase<int, string> Positive { get; } = Spec.Build((int n) => n > 0).Create("positive");

    private sealed class NumberRule() : Rule<int, string>("number", Positive);

    private const string NotPositive = """{"rule":{"not":{"spec":"positive"}}}""";
    private const string UnknownSpec = """{"rule":{"spec":"no-such-spec"}}""";
    private const string OnlyHere = """{"rule":{"spec":"only-here"}}""";

    private static RuleChangeProvenance By(string author) => new(author);

    private static SpecRegistry Registry() => new SpecRegistry().Register("positive", Positive);

    /// <summary>A replica whose build knows a spec the plain <see cref="Replica"/> build does not.</summary>
    /// <remarks>
    /// A replica is a <em>build</em> as much as a process, and the interesting refresh failures are
    /// the ones where two builds differ — a row published by a build that has since been rolled back.
    /// </remarks>
    private static RuleSet PublisherKnowing(IRuleStore store)
    {
        var rules = new RuleSet(
            new SpecRegistry().Register("positive", Positive).Register("only-here", Positive), store);
        rules.Add(new NumberRule());
        rules.Load();
        return rules;
    }

    private static RuleSet Replica(IRuleStore store)
    {
        var rules = new RuleSet(Registry(), store);
        rules.Add(new NumberRule());
        rules.Load();
        return rules;
    }

    /// <summary>
    /// Every reading of an instrument this test's own work produced. Counters are process-wide and the
    /// harness only listens while it is alive, so a fresh harness per test is the isolation — see
    /// <see cref="RulesTelemetryTestCollection"/> for why the classes are also serialized.
    /// </summary>
    private static double Total(RulesTelemetryHarness harness, string instrument) =>
        harness.For(instrument).Sum(measurement => measurement.Value);

    [Fact]
    public async Task Should_count_a_document_that_would_not_bind_as_a_publish_phase_bind_failure()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var rules = Replica(store);
        using var harness = new RulesTelemetryHarness();

        // Act
        var result = await rules.UpdateAsync("number", UnknownSpec, 1, By("alice"));

        // Assert
        result.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        var failure = harness.Single("motiv.rules.bind_failures");
        failure.Value.ShouldBe(1);
        failure.Tag("motiv.rules.kind").ShouldBe("rule");
        failure.Tag("motiv.rules.phase").ShouldBe("publish");
    }

    [Fact]
    public async Task Should_count_a_stale_expected_version_as_a_publish_conflict()
    {
        // Arrange — one publish lands, so the head has moved on past the version the second names.
        var store = new InMemoryRuleStore();
        var rules = Replica(store);
        await rules.UpdateAsync("number", NotPositive, 1, By("alice"));
        using var harness = new RulesTelemetryHarness();

        // Act
        var result = await rules.UpdateAsync("number", NotPositive, 1, By("bob"));

        // Assert
        result.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        var conflict = harness.Single("motiv.rules.publish_conflicts");
        conflict.Value.ShouldBe(1);
        conflict.Tag("motiv.rules.kind").ShouldBe("rule");
    }

    [Fact]
    public async Task Should_time_the_store_calls_a_publish_makes()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var rules = Replica(store);
        using var harness = new RulesTelemetryHarness();

        // Act
        await rules.UpdateAsync("number", NotPositive, 1, By("alice"));

        // Assert — the append is the write; the generation reads are what make the fencing token
        // store-derived rather than counted locally, and they are store round trips too.
        var operations = harness.For("motiv.rules.store.duration")
            .Select(measurement => measurement.Tag("motiv.rules.operation"))
            .Distinct();
        operations.ShouldBe(["generation", "append"], ignoreOrder: true);
        harness.For("motiv.rules.store.duration")
            .ShouldAllBe(measurement => measurement.Tag("motiv.rules.kind")!.Equals("rule"));
    }

    [Fact]
    public void Should_time_the_store_read_a_load_makes()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        using var harness = new RulesTelemetryHarness();

        // Act
        Replica(store);

        // Assert
        harness.For("motiv.rules.store.duration")
            .Select(measurement => measurement.Tag("motiv.rules.operation"))
            .ShouldContain("load");
    }

    [Fact]
    public async Task Should_count_a_row_quarantined_at_load_as_a_load_phase_bind_failure()
    {
        // Arrange — one replica publishes a document naming a spec a second build does not have.
        var store = new InMemoryRuleStore();
        var publisher = new RuleSet(
            new SpecRegistry().Register("positive", Positive).Register("only-here", Positive), store);
        publisher.Add(new NumberRule());
        publisher.Load();
        await publisher.UpdateAsync("number", """{"rule":{"spec":"only-here"}}""", 1, By("alice"));

        using var harness = new RulesTelemetryHarness();

        // Act — the second build loads the row and cannot bind it.
        var report = Replica(store);

        // Assert
        report.ShouldNotBeNull();
        var failure = harness.For("motiv.rules.bind_failures")
            .Single(measurement => measurement.Tag("motiv.rules.phase")!.Equals("load"));
        failure.Value.ShouldBe(1);
        failure.Tag("motiv.rules.kind").ShouldBe("rule");
    }

    [Fact]
    public async Task Should_count_a_refresh_by_what_it_did()
    {
        // Arrange — two replicas over one store, so one can publish and the other converge.
        var store = new InMemoryRuleStore();
        var a = Replica(store);
        var b = Replica(store);
        await a.UpdateAsync("number", NotPositive, 1, By("alice"));

        using var harness = new RulesTelemetryHarness();

        // Act
        (await b.RefreshAsync()).Outcome.ShouldBe(RefreshOutcome.Applied);
        (await b.RefreshAsync()).Outcome.ShouldBe(RefreshOutcome.Unchanged);

        // Assert
        var outcomes = harness.For("motiv.rules.refreshes")
            .Select(measurement => measurement.Tag("motiv.rules.outcome"));
        outcomes.ShouldBe(["applied", "unchanged"], ignoreOrder: true);
        Total(harness, "motiv.rules.refreshes").ShouldBe(2);
    }

    [Fact]
    public async Task Should_time_a_rebuild_but_not_a_tick_that_rebuilt_nothing()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var a = Replica(store);
        var b = Replica(store);
        await a.UpdateAsync("number", NotPositive, 1, By("alice"));

        using var harness = new RulesTelemetryHarness();

        // Act
        await b.RefreshAsync();          // applied — rebuilt a world
        await b.RefreshAsync();          // unchanged — built nothing

        // Assert — timing the no-op tick would report "no rebuild" as "a very fast rebuild", which is
        // the same number an operator would read as a healthy rebuild rate.
        harness.For("motiv.rules.rebuild.duration").Select(m => m.Tag("motiv.rules.outcome"))
            .ShouldBe(["applied"]);
    }

    [Fact]
    public async Task Should_report_the_generation_this_replica_serves_and_no_lag_once_converged()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var a = Replica(store);
        var b = Replica(store);
        await a.UpdateAsync("number", NotPositive, 1, By("alice"));
        await b.RefreshAsync();

        using var harness = new RulesTelemetryHarness();

        var served = await store.GetGenerationAsync(default);

        // Act
        harness.Collect();

        // Assert — the gauges enumerate every live scope in the process, so this asserts that b's
        // reading is among them rather than that it is the only one.
        harness.For("motiv.rules.generation")
            .ShouldContain(m => m.Tag("motiv.rules.store")!.Equals("rules") && m.Value == served);
        harness.For("motiv.rules.replica_lag")
            .ShouldContain(m => m.Tag("motiv.rules.store")!.Equals("rules") && m.Value == 0);
    }

    [Fact]
    public async Task Should_count_a_head_a_refresh_could_not_bind_as_a_refresh_phase_bind_failure()
    {
        // Arrange — a build that knows an extra spec publishes a document using it; a second build,
        // which does not know that spec, is already serving the rule and then refreshes onto the row.
        var store = new InMemoryRuleStore();
        var publisher = PublisherKnowing(store);
        var stranger = Replica(store);
        await publisher.UpdateAsync("number", OnlyHere, 1, By("alice"));

        using var harness = new RulesTelemetryHarness();

        // Act
        var report = await stranger.RefreshAsync();

        // Assert — the row has no live binding to protect, so it is carried quarantined rather than
        // blocking convergence. The refresh succeeded; a stored document still would not bind, and
        // that — not whether it stopped the tick — is what the instrument counts.
        report.Outcome.ShouldBe(RefreshOutcome.Applied);
        report.Quarantined.ShouldHaveSingleItem();
        var failure = harness.For("motiv.rules.bind_failures")
            .Single(measurement => measurement.Tag("motiv.rules.phase")!.Equals("refresh"));
        failure.Value.ShouldBe(1);
        failure.Tag("motiv.rules.kind").ShouldBe("rule");
    }

    [Fact]
    public async Task Should_count_a_row_that_is_still_broken_again_on_every_rebuild()
    {
        // Arrange — the same stuck row, and a store that moves twice so two rebuilds actually happen.
        var store = new InMemoryRuleStore();
        var publisher = PublisherKnowing(store);
        var stranger = Replica(store);
        await publisher.UpdateAsync("number", OnlyHere, 1, By("alice"));

        using var harness = new RulesTelemetryHarness();

        // Act
        (await stranger.RefreshAsync()).Outcome.ShouldBe(RefreshOutcome.Applied);
        await publisher.UpdateAsync("number", OnlyHere, 2, By("alice"));
        (await stranger.RefreshAsync()).Outcome.ShouldBe(RefreshOutcome.Applied);

        // Assert — counted per rebuild, on purpose. A row counted once and never again would leave a
        // stuck replica indistinguishable from a healthy one as soon as the first tick scrolled off
        // the dashboard; a rate that stays above zero is exactly the alert an operator wants.
        harness.For("motiv.rules.bind_failures")
            .Where(measurement => measurement.Tag("motiv.rules.phase")!.Equals("refresh"))
            .Sum(measurement => measurement.Value)
            .ShouldBe(2);
    }

    /// <summary>
    /// The counterpart to the test above, and the reason it is phrased "per rebuild" rather than
    /// "per tick": a tick that finds neither store moved examines nothing, so it has nothing to
    /// report. Counting the still-broken row there would mean inventing a finding out of a poll that
    /// never looked — and would make the instrument a function of the poll interval rather than of
    /// the catalog.
    /// </summary>
    [Fact]
    public async Task Should_report_nothing_from_a_tick_that_rebuilt_nothing()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var publisher = PublisherKnowing(store);
        var stranger = Replica(store);
        await publisher.UpdateAsync("number", OnlyHere, 1, By("alice"));
        await stranger.RefreshAsync();

        using var harness = new RulesTelemetryHarness();

        // Act
        (await stranger.RefreshAsync()).Outcome.ShouldBe(RefreshOutcome.Unchanged);

        // Assert
        harness.For("motiv.rules.bind_failures").ShouldBeEmpty();
    }

    [Fact]
    public void Should_report_the_catalog_size()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        Replica(store);

        using var harness = new RulesTelemetryHarness();

        // Act
        harness.Collect();

        // Assert
        harness.For("motiv.rules.catalog.size")
            .ShouldContain(m => m.Tag("motiv.rules.kind")!.Equals("rule") && m.Value == 1);
    }
}
