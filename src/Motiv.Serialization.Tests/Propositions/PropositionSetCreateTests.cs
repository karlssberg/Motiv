using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class PropositionSetCreateTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static AsyncSpecBase<Customer, string> PassesCheck { get; } =
        Spec.BuildAsync(async (Customer c) => { await Task.Yield(); return c.IsActive; })
            .WhenTrue("passes").WhenFalse("fails").Create();

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

    [Fact]
    public void Should_create_a_proposition_at_version_1()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = set.Create(
            "customer.is-eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", "Eligibility");

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        result.Version.ShouldBe(1);
    }

    [Fact]
    public void Should_make_a_created_proposition_resolvable_as_a_spec()
    {
        // Arrange — this is the whole point: an authored proposition becomes a building block
        var (set, scope, _) = NewSet();

        // Act
        set.Create("customer.is-eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Assert
        var entry = scope.Source.Find("customer.is-eligible");
        entry.ShouldNotBeNull();
        entry.ModelType.ShouldBe(typeof(Customer));
        entry.MetadataType.ShouldBe(typeof(string));
        entry.IsAsync.ShouldBeFalse();
    }

    [Fact]
    public void Should_let_a_proposition_reference_another_proposition()
    {
        // Arrange
        var (set, scope, _) = NewSet();
        set.Create("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Act
        var result = set.Create("customer.b", "customer", """{ "rule": { "not": { "spec": "customer.a" } } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        scope.Source.Find("customer.b").ShouldNotBeNull();
    }

    [Fact]
    public void Should_derive_asyncness_from_the_referenced_specs()
    {
        // Arrange
        var (set, scope, _) = NewSet();

        // Act
        set.Create("customer.screened", "customer", """{ "rule": { "spec": "customer.passes-check" } }""", null);

        // Assert
        scope.Source.Find("customer.screened")!.IsAsync.ShouldBeTrue();
    }

    [Fact]
    public void Should_propagate_asyncness_transitively_through_a_proposition()
    {
        // Arrange — b references a, which is async; b must be async too
        var (set, scope, _) = NewSet();
        set.Create("customer.a", "customer", """{ "rule": { "spec": "customer.passes-check" } }""", null);

        // Act
        set.Create("customer.b", "customer", """{ "rule": { "not": { "spec": "customer.a" } } }""", null);

        // Assert
        scope.Source.Find("customer.b")!.IsAsync.ShouldBeTrue();
    }

    [Fact]
    public void Should_persist_a_created_proposition()
    {
        // Arrange
        var (set, _, store) = NewSet();

        // Act
        set.Create("customer.is-eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", "why");

        // Assert
        var stored = store.Load();
        stored.Count.ShouldBe(1);
        stored[0].Name.ShouldBe("customer.is-eligible");
        stored[0].ModelType.ShouldBe("customer");
        stored[0].Version.ShouldBe(1);
        stored[0].Description!.ShouldBe("why");
    }

    [Fact]
    public void Should_reject_a_name_that_violates_the_grammar()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = set.Create("customer..bad", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.InvalidSpecName);
    }

    [Fact]
    public void Should_reject_a_name_already_authored()
    {
        // Arrange
        var (set, _, _) = NewSet();
        set.Create("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Act
        var result = set.Create("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.NameTaken);
    }

    [Fact]
    public void Should_accept_a_name_that_exists_only_as_a_compiled_spec_creating_an_override()
    {
        // Arrange — overriding a compiled spec is a create, and must not read as a name clash
        var (set, scope, _) = NewSet();

        // Act
        var result = set.Create(
            "customer.is-active", "customer", """{ "rule": { "not": { "spec": "customer.passes-check" } } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        result.Version.ShouldBe(1);
        set.Find("customer.is-active")!.Origin.ShouldBe(PropositionOrigin.Overridden);
        // The overlay now shadows the compiled spec, so the effective definition is the async one.
        scope.Source.Find("customer.is-active")!.IsAsync.ShouldBeTrue();
    }

    [Fact]
    public void Should_reject_a_self_reference()
    {
        // Arrange — the only cycle a brand-new name can create
        var (set, _, _) = NewSet();

        // Act
        var result = set.Create("customer.a", "customer", """{ "rule": { "spec": "customer.a" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.CycleDetected);
    }

    [Fact]
    public void Should_reject_a_document_referencing_an_unknown_spec()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = set.Create("customer.a", "customer", """{ "rule": { "spec": "nope" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public void Should_reject_an_unregistered_model_type()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = set.Create("order.a", "order", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.ModelTypeMismatch);
    }

    [Fact]
    public void Should_not_persist_or_publish_a_rejected_create()
    {
        // Arrange
        var (set, scope, store) = NewSet();

        // Act
        set.Create("customer.a", "customer", """{ "rule": { "spec": "nope" } }""", null);

        // Assert
        store.Load().ShouldBeEmpty();
        scope.Source.Find("customer.a").ShouldBeNull();
        set.Find("customer.a").ShouldBeNull();
    }

    [Fact]
    public void Should_list_compiled_and_authored_propositions_together()
    {
        // Arrange
        var (set, _, _) = NewSet();
        set.Create("customer.is-eligible", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

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
    public void Should_report_the_document_of_an_authored_proposition_and_null_for_a_compiled_one()
    {
        // Arrange
        var (set, _, _) = NewSet();
        set.Create("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Act & Assert
        set.DocumentJsonOf("customer.a").ShouldNotBeNull();
        set.DocumentJsonOf("customer.is-active").ShouldBeNull();
    }

    [Fact]
    public void Should_publish_nothing_when_the_store_refuses_to_persist()
    {
        // Arrange
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var scope = new BindingScope(registry);
        var set = new PropositionSet(scope, new ThrowingStore()).AddModel<Customer>("customer");

        // Act
        var create = () => set.Create(
            "customer.derived", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Assert — the failure surfaces, and nothing is left live behind it
        create.ShouldThrow<IOException>();
        set.Find("customer.derived").ShouldBeNull();
        scope.Source.Find("customer.derived").ShouldBeNull();
        scope.Graph.Referrers("customer.is-active").ShouldBeEmpty();
    }

    private sealed record Customer(bool IsActive);

    /// <summary>A store that refuses to persist, standing in for a full disk or a database outage.</summary>
    private sealed class ThrowingStore : IPropositionStore
    {
        public IReadOnlyList<StoredProposition> Load() => [];
        public void Save(StoredProposition proposition) => throw new IOException("store unavailable");
        public void Delete(string name) { }
    }
}
