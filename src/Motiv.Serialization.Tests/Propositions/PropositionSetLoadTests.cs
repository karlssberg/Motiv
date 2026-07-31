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

    private static (PropositionSet Set, BindingScope Scope) Load(params StoredProposition[] stored)
    {
        var store = new InMemoryPropositionStore();
        foreach (var proposition in stored)
            store.Save(proposition);

        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var set = new PropositionSet(scope, store).AddModel<Customer>("customer");
        set.Load();
        return (set, scope);
    }

    [Fact]
    public void Should_bind_a_stored_proposition()
    {
        // Act
        var (set, scope) = Load(Stored("customer.a", """{ "rule": { "spec": "customer.is-active" } }"""));

        // Assert
        scope.Source.Find("customer.a").ShouldNotBeNull();
        set.Find("customer.a")!.Quarantine.ShouldBeEmpty();
    }

    [Fact]
    public void Should_preserve_the_stored_version()
    {
        // Act
        var (set, _) = Load(Stored("customer.a", """{ "rule": { "spec": "customer.is-active" } }""", version: 7));

        // Assert — versions must survive a restart or every reader's next save would conflict
        set.Find("customer.a")!.Version.ShouldBe(7);
    }

    [Fact]
    public void Should_bind_dependencies_before_dependents_regardless_of_store_order()
    {
        // Arrange — b depends on a, deliberately stored first
        var stored = new[]
        {
            Stored("customer.b", """{ "rule": { "spec": "customer.a" } }"""),
            Stored("customer.a", """{ "rule": { "spec": "customer.is-active" } }"""),
        };

        // Act
        var (set, scope) = Load(stored);

        // Assert
        scope.Source.Find("customer.b").ShouldNotBeNull();
        set.Find("customer.b")!.Quarantine.ShouldBeEmpty();
    }

    [Fact]
    public void Should_quarantine_a_document_referencing_a_spec_that_no_longer_exists()
    {
        // Arrange — the redeploy case: the C# spec this document referenced was renamed away
        // Act
        var (set, scope) = Load(Stored("customer.a", """{ "rule": { "spec": "customer.removed-in-a-redeploy" } }"""));

        // Assert
        var entry = set.Find("customer.a").ShouldNotBeNull();
        entry.Quarantine.ShouldContain(error => error.Code == RuleErrorCode.UnknownSpec);
        scope.Source.Find("customer.a").ShouldBeNull();
    }

    [Fact]
    public void Should_keep_the_document_of_a_quarantined_proposition_for_repair()
    {
        // Act
        var (set, _) = Load(Stored("customer.a", """{ "rule": { "spec": "gone" } }"""));

        // Assert
        set.DocumentJsonOf("customer.a").ShouldNotBeNull();
    }

    [Fact]
    public void Should_quarantine_a_dependent_of_a_quarantined_proposition()
    {
        // Arrange
        var stored = new[]
        {
            Stored("customer.a", """{ "rule": { "spec": "gone" } }"""),
            Stored("customer.b", """{ "rule": { "spec": "customer.a" } }"""),
        };

        // Act
        var (set, scope) = Load(stored);

        // Assert
        set.Find("customer.b")!.Quarantine.ShouldNotBeEmpty();
        scope.Source.Find("customer.b").ShouldBeNull();
    }

    [Fact]
    public void Should_let_a_compiled_spec_resolve_beneath_a_quarantined_override()
    {
        // Arrange — a broken override must reveal the compiled spec, not a hole
        // Act
        var (set, scope) = Load(Stored("customer.is-active", """{ "rule": { "spec": "gone" } }"""));

        // Assert
        set.Find("customer.is-active")!.Quarantine.ShouldNotBeEmpty();
        var entry = scope.Source.Find("customer.is-active").ShouldNotBeNull();
        entry.Spec.ShouldBeSameAs(IsActive);
    }

    [Fact]
    public void Should_load_the_healthy_propositions_alongside_the_quarantined_ones()
    {
        // Arrange — one bad row must not cost the whole store
        var stored = new[]
        {
            Stored("customer.broken", """{ "rule": { "spec": "gone" } }"""),
            Stored("customer.fine", """{ "rule": { "spec": "customer.is-active" } }"""),
        };

        // Act
        var (set, scope) = Load(stored);

        // Assert
        scope.Source.Find("customer.fine").ShouldNotBeNull();
        scope.Source.Find("customer.broken").ShouldBeNull();
    }

    [Fact]
    public void Should_never_throw_on_a_malformed_stored_document()
    {
        // Arrange — a hand-edited JSON file must not stop the application booting
        var store = new InMemoryPropositionStore();
        store.Save(Stored("customer.a", "{ not json"));
        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var set = new PropositionSet(scope, store).AddModel<Customer>("customer");

        // Act
        var load = () => set.Load();

        // Assert
        load.ShouldNotThrow();
        set.Find("customer.a")!.Quarantine.ShouldNotBeEmpty();
    }

    [Fact]
    public void Should_allow_repairing_a_quarantined_proposition_by_updating_it()
    {
        // Arrange
        var (set, scope) = Load(Stored("customer.a", """{ "rule": { "spec": "gone" } }""", version: 3));

        // Act
        var result = set.Update("customer.a", """{ "rule": { "spec": "customer.is-active" } }""", 3);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        set.Find("customer.a")!.Quarantine.ShouldBeEmpty();
        scope.Source.Find("customer.a").ShouldNotBeNull();
    }

    [Fact]
    public void Should_allow_deleting_a_quarantined_proposition()
    {
        // Arrange
        var (set, _) = Load(Stored("customer.a", """{ "rule": { "spec": "gone" } }""", version: 2));

        // Act
        var result = set.Withdraw("customer.a", 2);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
        set.Find("customer.a").ShouldBeNull();
    }

    [Fact]
    public void Should_quarantine_both_members_of_a_cycle_in_the_store()
    {
        // Arrange — a hand-edited store can contain a reference cycle that Create/Update, which run
        // DependencyGraph.FindCycle, would have rejected outright
        var store = new InMemoryPropositionStore();
        store.Save(Stored("customer.is-active", """{ "rule": { "spec": "customer.is-eligible" } }"""));
        store.Save(Stored("customer.is-eligible", """{ "rule": { "spec": "customer.is-active" } }"""));

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

    [Fact]
    public void Should_never_throw_when_a_stored_document_is_null()
    {
        // Arrange — System.Text.Json will happily populate this non-nullable property with `null`
        // from a `"documentJson": null` row; JsonDocument.Parse(null, ...) then throws
        // ArgumentNullException, which is not a JsonException and is not caught inside Parse itself
        var store = new InMemoryPropositionStore();
        store.Save(Stored("customer.a", null!));
        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var set = new PropositionSet(scope, store).AddModel<Customer>("customer");

        // Act
        var load = () => set.Load();

        // Assert
        load.ShouldNotThrow();
        set.Find("customer.a")!.Quarantine.ShouldNotBeEmpty();
    }

    [Fact]
    public void Should_quarantine_a_stored_proposition_with_an_invalid_name()
    {
        // Arrange — a hand-edited store is not bound by the grammar Create enforces
        // Act
        var (set, scope) = Load(Stored("1bad", """{ "rule": { "spec": "customer.is-active" } }"""));

        // Assert
        set.Find("1bad")!.Quarantine.ShouldContain(error => error.Code == RuleErrorCode.InvalidSpecName);
        scope.Source.Find("1bad").ShouldBeNull();
    }

    private sealed record Customer(bool IsActive);
}
