using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class PropositionSetCreateTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static AsyncSpecBase<Customer, string> PassesCheck { get; } =
        Spec.BuildAsync(async (Customer c) => { await Task.Yield(); return c.IsActive; })
            .WhenTrue("passes").WhenFalse("fails").Create();

    private static SpecBase<Customer, string> IsInactive { get; } =
        Spec.Build((Customer c) => !c.IsActive).WhenTrue("inactive").WhenFalse("active").Create();

    private static (PropositionSet Set, BindingScope Scope, InMemoryPropositionStore Store) NewSet()
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.passes-check", PassesCheck);
        var scope = new BindingScope(registry);
        var store = new InMemoryPropositionStore();
        var set = new PropositionSet(scope, store).AddModel<Customer>("customer");
        return (set, scope, store);
    }

    /// <summary>
    /// A host for the override tests: two compiled specs that disagree, so overriding one with the
    /// other is observable. Separate from <see cref="NewSet"/>, whose spec listing is asserted on
    /// by name and by count.
    /// </summary>
    private static (PropositionSet Set, BindingScope Scope) NewOverridableSet(
        InMemoryPropositionStore? store = null)
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-inactive", IsInactive);
        var scope = new BindingScope(registry);
        var set = new PropositionSet(scope, store ?? new InMemoryPropositionStore())
            .AddModel<Customer>("customer");
        return (set, scope);
    }

    /// <summary>Evaluates whatever the layered source currently resolves for a name.</summary>
    private static bool Evaluate(BindingScope scope, string name, Customer customer)
    {
        var entry = scope.Source.Find(name).ShouldNotBeNull();
        return ((SpecBase<Customer, string>)entry.Spec).Evaluate(customer).Satisfied;
    }

    /// <summary>A participant that refuses to rebind, standing in for a rule that would break.</summary>
    private sealed class AlwaysBreaks(NodeId node) : IRebindable
    {
        public NodeId Node { get; } = node;

        public IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors)
        {
            errors.Add(new RuleError("$", RuleErrorCode.AsyncSpecInSyncLoad, "would not bind"));
            return null;
        }
    }

    [Fact]
    public async Task Should_create_a_proposition_at_version_1()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = await set.CreateAsync(
            "customer.is-eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", "Eligibility");

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        result.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Should_make_a_created_proposition_resolvable_as_a_spec()
    {
        // Arrange — this is the whole point: an authored proposition becomes a building block
        var (set, scope, _) = NewSet();

        // Act
        await set.CreateAsync("customer.is-eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Assert
        var entry = scope.Source.Find("customer.is-eligible");
        entry.ShouldNotBeNull();
        entry.ModelType.ShouldBe(typeof(Customer));
        entry.MetadataType.ShouldBe(typeof(string));
        entry.IsAsync.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_let_a_proposition_reference_another_proposition()
    {
        // Arrange
        var (set, scope, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Act
        var result = await set.CreateAsync("customer.b", "customer", """{ "rule": { "not": { "spec": "customer.a" } } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        scope.Source.Find("customer.b").ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_derive_asyncness_from_the_referenced_specs()
    {
        // Arrange
        var (set, scope, _) = NewSet();

        // Act
        await set.CreateAsync("customer.screened", "customer", """{ "rule": { "spec": "customer.passes-check" } }""", null);

        // Assert
        scope.Source.Find("customer.screened")!.IsAsync.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_propagate_asyncness_transitively_through_a_proposition()
    {
        // Arrange — b references a, which is async; b must be async too
        var (set, scope, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.passes-check" } }""", null);

        // Act
        await set.CreateAsync("customer.b", "customer", """{ "rule": { "not": { "spec": "customer.a" } } }""", null);

        // Assert
        scope.Source.Find("customer.b")!.IsAsync.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_persist_a_created_proposition()
    {
        // Arrange
        var (set, _, store) = NewSet();

        // Act
        await set.CreateAsync("customer.is-eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", "why");

        // Assert
        var stored = store.Load();
        stored.Count.ShouldBe(1);
        stored[0].Name.ShouldBe("customer.is-eligible");
        stored[0].ModelType.ShouldBe("customer");
        stored[0].Version.ShouldBe(1);
        stored[0].Description!.ShouldBe("why");
    }

    [Fact]
    public async Task Should_reject_a_name_that_violates_the_grammar()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = await set.CreateAsync("customer..bad", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.InvalidSpecName);
    }

    [Fact]
    public async Task Should_reject_a_name_already_authored()
    {
        // Arrange
        var (set, _, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Act
        var result = await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.NameTaken);
    }

    [Fact]
    public async Task Should_accept_a_name_that_exists_only_as_a_compiled_spec_creating_an_override()
    {
        // Arrange — overriding a compiled spec is a create, and must not read as a name clash
        var (set, scope, _) = NewSet();

        // Act
        var result = await set.CreateAsync(
            "customer.is-active", "customer", """{ "rule": { "not": { "spec": "customer.passes-check" } } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        result.Version.ShouldBe(1);
        set.Find("customer.is-active")!.Origin.ShouldBe(PropositionOrigin.Overridden);
        // The overlay now shadows the compiled spec, so the effective definition is the async one.
        scope.Source.Find("customer.is-active")!.IsAsync.ShouldBeTrue();
    }

    /// <summary>
    /// An override is the one create that lands on a name something may already reference, so it is
    /// the one create that has to cascade. Without this the overlay entry is published and the
    /// catalog reports the override, while every dependent goes on resolving the compiled spec —
    /// and only a later, redundant <see cref="PropositionSet.UpdateAsync"/> makes the override take.
    /// </summary>
    [Fact]
    public async Task Should_rebind_a_dependent_when_a_create_overrides_the_compiled_spec_it_references()
    {
        // Arrange — derived is bound against the *compiled* customer.is-active
        var (set, scope) = NewOverridableSet();
        await set.CreateAsync("customer.derived", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        var active = new Customer(IsActive: true);
        Evaluate(scope, "customer.derived", active).ShouldBeTrue();

        // Act — the name derived references is overridden to mean the opposite; derived is not touched
        var result = await set.CreateAsync(
            "customer.is-active", "customer", """{ "rule": { "spec": "customer.is-inactive" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        Evaluate(scope, "customer.derived", active).ShouldBeFalse();
    }

    /// <summary>
    /// The transactional half of the same rule: if an override cascades, it must also be refused
    /// whole when the cascade would break something, exactly as an update is.
    /// </summary>
    [Fact]
    public async Task Should_reject_the_whole_create_when_an_override_would_break_a_dependent()
    {
        // Arrange — a stubbed dependent stands in for a rule that cannot bind the overriding definition
        var (set, scope) = NewOverridableSet();
        scope.Locked(() =>
        {
            scope.Enrol(new AlwaysBreaks(NodeId.Rule("can-checkout")));
            scope.Graph.Set(NodeId.Rule("can-checkout"), ["customer.is-active"]);
        });

        // Act
        var result = await set.CreateAsync(
            "customer.is-active", "customer", """{ "rule": { "spec": "customer.is-inactive" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.BrokenDependents.Count.ShouldBe(1);
        result.BrokenDependents[0].Name.ShouldBe("can-checkout");
        result.BrokenDependents[0].Kind.ShouldBe("rule");
    }

    [Fact]
    public async Task Should_leave_everything_untouched_when_an_override_would_break_a_dependent()
    {
        // Arrange
        var store = new InMemoryPropositionStore();
        var (set, scope) = NewOverridableSet(store);
        scope.Locked(() =>
        {
            scope.Enrol(new AlwaysBreaks(NodeId.Rule("can-checkout")));
            scope.Graph.Set(NodeId.Rule("can-checkout"), ["customer.is-active"]);
        });
        var active = new Customer(IsActive: true);

        // Act
        await set.CreateAsync("customer.is-active", "customer", """{ "rule": { "spec": "customer.is-inactive" } }""", null);

        // Assert — nothing authored, nothing persisted, the compiled spec still resolving
        set.Find("customer.is-active")!.Origin.ShouldBe(PropositionOrigin.Compiled);
        set.DocumentJsonOf("customer.is-active").ShouldBeNull();
        store.Load().ShouldBeEmpty();
        Evaluate(scope, "customer.is-active", active).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_reject_a_self_reference()
    {
        // Arrange — the only cycle a brand-new name can create
        var (set, _, _) = NewSet();

        // Act
        var result = await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.a" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.CycleDetected);
    }

    [Fact]
    public async Task Should_reject_a_document_referencing_an_unknown_spec()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "nope" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public async Task Should_reject_an_unregistered_model_type()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = await set.CreateAsync("order.a", "order", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.ModelTypeMismatch);
    }

    [Fact]
    public async Task Should_not_persist_or_publish_a_rejected_create()
    {
        // Arrange
        var (set, scope, store) = NewSet();

        // Act
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "nope" } }""", null);

        // Assert
        store.Load().ShouldBeEmpty();
        scope.Source.Find("customer.a").ShouldBeNull();
        set.Find("customer.a").ShouldBeNull();
    }

    [Fact]
    public async Task Should_list_compiled_and_authored_propositions_together()
    {
        // Arrange
        var (set, _, _) = NewSet();
        await set.CreateAsync("customer.is-eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Act
        var listed = set.Propositions.ToDictionary(entry => entry.Name);

        // Assert
        listed.Count.ShouldBe(3);
        listed["customer.is-active"].Origin.ShouldBe(PropositionOrigin.Compiled);
        listed["customer.is-active"].Version.ShouldBe(0);
        listed["customer.is-active"].ModelType.ShouldBe("customer");
        listed["customer.passes-check"].Origin.ShouldBe(PropositionOrigin.Compiled);
        listed["customer.passes-check"].ModelType.ShouldBe("customer");
        listed["customer.is-eligible"].Origin.ShouldBe(PropositionOrigin.Authored);
        listed["customer.is-eligible"].Version.ShouldBe(1);
        listed["customer.is-eligible"].ModelType.ShouldBe("customer");
    }

    [Fact]
    public async Task Should_report_the_document_of_an_authored_proposition_and_null_for_a_compiled_one()
    {
        // Arrange
        var (set, _, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Act & Assert
        set.DocumentJsonOf("customer.a").ShouldNotBeNull();
        set.DocumentJsonOf("customer.is-active").ShouldBeNull();
    }

    [Fact]
    public async Task Should_publish_nothing_when_the_store_refuses_to_persist()
    {
        // Arrange
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var scope = new BindingScope(registry);
        var set = new PropositionSet(scope, new ThrowingStore()).AddModel<Customer>("customer");

        // Act — the failure surfaces, and nothing is left live behind it
        await Should.ThrowAsync<IOException>(async () => await set.CreateAsync(
            "customer.derived", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null));

        // Assert
        set.Find("customer.derived").ShouldBeNull();
        scope.Source.Find("customer.derived").ShouldBeNull();
        scope.Graph.Referrers("customer.is-active").ShouldBeEmpty();
    }

    private sealed record Customer(bool IsActive);

    /// <summary>A store that refuses to persist, standing in for a full disk or a database outage.</summary>
    private sealed class ThrowingStore : IPropositionStore
    {
        public IReadOnlyList<StoredProposition> Load() => [];
        public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StoredProposition>>([]);
        public Task<long> GetGenerationAsync(CancellationToken ct) => Task.FromResult(0L);
        public Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken) =>
            throw new IOException("store unavailable");
    }
}
