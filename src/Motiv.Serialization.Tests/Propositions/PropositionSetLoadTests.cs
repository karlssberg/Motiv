using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class PropositionSetLoadTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsEligible { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("eligible").WhenFalse("not eligible").Create();

    private static StoredProposition Stored(string name, string documentJson, int version = 1) =>
        new(name, "customer", documentJson, version, null);

    private static async Task<(PropositionSet Set, BindingScope Scope)> Load(params StoredProposition[] stored)
    {
        var store = new InMemoryPropositionStore();
        foreach (var proposition in stored)
            await store.WriteAsync(PropositionBatch.Save(proposition), default);

        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var set = new PropositionSet(scope, store).AddModel<Customer>("customer");
        set.Load();
        return (set, scope);
    }

    [Fact]
    public async Task Should_bind_a_stored_proposition()
    {
        // Act
        var (set, scope) = await Load(Stored("customer.a", """{ "rule": { "spec": "customer.is-active" } }"""));

        // Assert
        scope.Source.Find("customer.a").ShouldNotBeNull();
        set.Find("customer.a")!.Quarantine.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_preserve_the_stored_version()
    {
        // Act
        var (set, _) = await Load(Stored("customer.a", """{ "rule": { "spec": "customer.is-active" } }""", version: 7));

        // Assert — versions must survive a restart or every reader's next save would conflict
        set.Find("customer.a")!.Version.ShouldBe(7);
    }

    [Fact]
    public async Task Should_bind_dependencies_before_dependents_regardless_of_store_order()
    {
        // Arrange — b depends on a, deliberately stored first
        var stored = new[]
        {
            Stored("customer.b", """{ "rule": { "spec": "customer.a" } }"""),
            Stored("customer.a", """{ "rule": { "spec": "customer.is-active" } }"""),
        };

        // Act
        var (set, scope) = await Load(stored);

        // Assert
        scope.Source.Find("customer.b").ShouldNotBeNull();
        set.Find("customer.b")!.Quarantine.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_quarantine_a_document_referencing_a_spec_that_no_longer_exists()
    {
        // Arrange — the redeploy case: the C# spec this document referenced was renamed away
        // Act
        var (set, scope) = await Load(Stored("customer.a", """{ "rule": { "spec": "customer.removed-in-a-redeploy" } }"""));

        // Assert
        var entry = set.Find("customer.a").ShouldNotBeNull();
        entry.Quarantine.ShouldContain(error => error.Code == RuleErrorCode.UnknownSpec);
        scope.Source.Find("customer.a").ShouldBeNull();
    }

    [Fact]
    public async Task Should_keep_the_document_of_a_quarantined_proposition_for_repair()
    {
        // Act
        var (set, _) = await Load(Stored("customer.a", """{ "rule": { "spec": "gone" } }"""));

        // Assert
        set.DocumentJsonOf("customer.a").ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_quarantine_a_dependent_of_a_quarantined_proposition()
    {
        // Arrange
        var stored = new[]
        {
            Stored("customer.a", """{ "rule": { "spec": "gone" } }"""),
            Stored("customer.b", """{ "rule": { "spec": "customer.a" } }"""),
        };

        // Act
        var (set, scope) = await Load(stored);

        // Assert — b is quarantined for the right reason: a never bound, so it never reached the
        // overlay, and b's own reference to it is what fails to resolve
        set.Find("customer.b")!.Quarantine.ShouldContain(error =>
            error.Code == RuleErrorCode.UnknownSpec && error.Message.Contains("customer.a"));
        scope.Source.Find("customer.b").ShouldBeNull();
    }

    [Fact]
    public async Task Should_let_a_compiled_spec_resolve_beneath_a_quarantined_override()
    {
        // Arrange — a broken override must reveal the compiled spec, not a hole
        // Act
        var (set, scope) = await Load(Stored("customer.is-active", """{ "rule": { "spec": "gone" } }"""));

        // Assert
        set.Find("customer.is-active")!.Quarantine.ShouldNotBeEmpty();
        var entry = scope.Source.Find("customer.is-active").ShouldNotBeNull();
        entry.Spec.ShouldBeSameAs(IsActive);
    }

    [Fact]
    public async Task Should_load_the_healthy_propositions_alongside_the_quarantined_ones()
    {
        // Arrange — one bad row must not cost the whole store
        var stored = new[]
        {
            Stored("customer.broken", """{ "rule": { "spec": "gone" } }"""),
            Stored("customer.fine", """{ "rule": { "spec": "customer.is-active" } }"""),
        };

        // Act
        var (set, scope) = await Load(stored);

        // Assert
        scope.Source.Find("customer.fine").ShouldNotBeNull();
        scope.Source.Find("customer.broken").ShouldBeNull();
    }

    [Fact]
    public async Task Should_never_throw_on_a_malformed_stored_document()
    {
        // Arrange — a hand-edited JSON file must not stop the application booting
        var store = new InMemoryPropositionStore();
        await store.WriteAsync(PropositionBatch.Save(Stored("customer.a", "{ not json")), default);
        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var set = new PropositionSet(scope, store).AddModel<Customer>("customer");

        // Act
        var load = () => set.Load();

        // Assert
        load.ShouldNotThrow();
        set.Find("customer.a")!.Quarantine.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Should_allow_repairing_a_quarantined_proposition_by_updating_it()
    {
        // Arrange
        var (set, scope) = await Load(Stored("customer.a", """{ "rule": { "spec": "gone" } }""", version: 3));

        // Act
        var result = await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-active" } }""", 3);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        set.Find("customer.a")!.Quarantine.ShouldBeEmpty();
        scope.Source.Find("customer.a").ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_allow_deleting_a_quarantined_proposition()
    {
        // Arrange
        var (set, _) = await Load(Stored("customer.a", """{ "rule": { "spec": "gone" } }""", version: 2));

        // Act
        var result = await set.WithdrawAsync("customer.a", 2);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
        set.Find("customer.a").ShouldBeNull();
    }

    [Fact]
    public async Task Should_quarantine_both_members_of_a_cycle_in_the_store()
    {
        // Arrange — a hand-edited store can contain a reference cycle that Create/Update, which run
        // DependencyGraph.FindCycle, would have rejected outright
        var store = new InMemoryPropositionStore();
        await store.WriteAsync(
            PropositionBatch.Save(Stored("customer.is-active", """{ "rule": { "spec": "customer.is-eligible" } }""")),
            default);
        await store.WriteAsync(
            PropositionBatch.Save(Stored("customer.is-eligible", """{ "rule": { "spec": "customer.is-active" } }""")),
            default);

        var scope = new BindingScope(new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-eligible", IsEligible));
        var set = new PropositionSet(scope, store).AddModel<Customer>("customer");

        // Act
        set.Load();

        // Assert — both are quarantined for the real reason, and the compiled spec beneath each
        // name is what resolves, not a hole and not the other cyclic document
        set.Find("customer.is-active")!.Quarantine.ShouldContain(error => error.Code == RuleErrorCode.CycleDetected);
        set.Find("customer.is-eligible")!.Quarantine.ShouldContain(error => error.Code == RuleErrorCode.CycleDetected);
        scope.Source.Find("customer.is-active")!.Spec.ShouldBeSameAs(IsActive);
        scope.Source.Find("customer.is-eligible")!.Spec.ShouldBeSameAs(IsEligible);
    }

    /// <summary>
    /// A document with no <c>rule</c> at all parses to a <see cref="RuleDocument"/> with a null root,
    /// and Load reads its references *outside* SafeParse's try/catch — so
    /// <c>DocumentReferences.From</c>'s null-root guard is the only thing between a <c>{}</c> row and
    /// a NullReferenceException out of startup.
    /// </summary>
    [Fact]
    public async Task Should_quarantine_a_stored_document_with_no_rule()
    {
        // Act
        var (set, scope) = await Load(Stored("customer.a", "{ }"));

        // Assert
        set.Find("customer.a")!.Quarantine.ShouldContain(error => error.Code == RuleErrorCode.InvalidNode);
        scope.Source.Find("customer.a").ShouldBeNull();
    }

    /// <summary>The shapes a hand-edited or null-serialized store row can arrive in.</summary>
    public enum MalformedStore
    {
        /// <summary>Load() itself returns null.</summary>
        NullList,

        /// <summary>A null element in the list — <c>Deserialize&lt;List&lt;StoredProposition&gt;&gt;("[null]")</c>.</summary>
        NullRow,

        /// <summary>A row whose name is null, so it cannot even be keyed by name.</summary>
        NullName,

        /// <summary>A row whose model-type id is null.</summary>
        NullModelType,

        /// <summary>A row whose document JSON is null.</summary>
        NullDocument
    }

    /// <summary>A store that hands back exactly what it was given, nulls included.</summary>
    private sealed class RawStore(IReadOnlyList<StoredProposition>? rows) : IPropositionStore
    {
        public IReadOnlyList<StoredProposition> Load() => rows!;
        public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken ct) => Task.FromResult(Load());
        public Task<long> GetGenerationAsync(CancellationToken ct) => Task.FromResult(0L);

        public Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Theory]
    [InlineData(MalformedStore.NullList)]
    [InlineData(MalformedStore.NullRow)]
    [InlineData(MalformedStore.NullName)]
    [InlineData(MalformedStore.NullModelType)]
    [InlineData(MalformedStore.NullDocument)]
    public void Should_never_throw_when_the_store_yields_a_malformed_row(MalformedStore shape)
    {
        // Arrange — quarantine exists so one bad row is never fatal, and a hand-edited store is not
        // bound by the non-nullable property types. `Deserialize<List<StoredProposition>>("[null]")`
        // really does yield a one-element list holding null, and a `"documentJson": null` row
        // reaches JsonDocument.Parse(null, ...), which throws ArgumentNullException rather than the
        // JsonException Parse itself guards against.
        const string document = """{ "rule": { "spec": "customer.is-active" } }""";
        IReadOnlyList<StoredProposition>? rows = shape switch
        {
            MalformedStore.NullList => null,
            MalformedStore.NullRow => [null!],
            MalformedStore.NullName => [new StoredProposition(null!, "customer", document, 1, null)],
            MalformedStore.NullModelType => [new StoredProposition("customer.a", null!, document, 1, null)],
            MalformedStore.NullDocument => [new StoredProposition("customer.a", "customer", null!, 1, null)],
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };

        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var set = new PropositionSet(scope, new RawStore(rows)).AddModel<Customer>("customer");

        // Act
        var load = () => set.Load();

        // Assert — none of these may stop the host booting
        load.ShouldNotThrow();

        // A row carrying a usable name is recorded, quarantined, so it can be found and repaired.
        // The two rows with no usable name have nowhere to be recorded, so they are simply skipped.
        switch (shape)
        {
            case MalformedStore.NullModelType:
                set.Find("customer.a")!.Quarantine
                    .ShouldContain(error => error.Code == RuleErrorCode.ModelTypeMismatch);
                break;
            case MalformedStore.NullDocument:
                set.Find("customer.a")!.Quarantine
                    .ShouldContain(error => error.Code == RuleErrorCode.InvalidNode);
                break;
            default:
                set.Propositions.ShouldAllBe(entry => entry.Origin == PropositionOrigin.Compiled);
                break;
        }
    }

    /// <summary>
    /// Load is a startup step, not an idempotent refresh. A second call cannot re-run cleanly: a row
    /// that quarantines the second time round has already had its overlay entry and its graph edges
    /// written by the first, and the quarantine path clears neither — so the catalog would report the
    /// proposition broken while the evaluator carried on resolving the stale binding.
    /// </summary>
    [Fact]
    public async Task Should_refuse_a_second_load()
    {
        // Arrange
        var (set, scope) = await Load(Stored("customer.a", """{ "rule": { "spec": "customer.is-active" } }"""));

        // Act
        var second = () => set.Load();

        // Assert
        second.ShouldThrow<InvalidOperationException>();

        // The first load stands: refusing the second must not have disturbed it
        scope.Source.Find("customer.a").ShouldNotBeNull();
        set.Find("customer.a")!.Quarantine.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_quarantine_a_stored_proposition_with_an_invalid_name()
    {
        // Arrange — a hand-edited store is not bound by the grammar Create enforces
        // Act
        var (set, scope) = await Load(Stored("1bad", """{ "rule": { "spec": "customer.is-active" } }"""));

        // Assert
        set.Find("1bad")!.Quarantine.ShouldContain(error => error.Code == RuleErrorCode.InvalidSpecName);
        scope.Source.Find("1bad").ShouldBeNull();
    }

    private sealed record Customer(bool IsActive);
}
