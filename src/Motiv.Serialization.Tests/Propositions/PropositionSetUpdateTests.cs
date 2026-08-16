using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class PropositionSetUpdateTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private static AsyncSpecBase<Customer, string> PassesCheck { get; } =
        Spec.BuildAsync(async (Customer c) => { await Task.Yield(); return c.IsActive; })
            .WhenTrue("passes").WhenFalse("fails").Create();

    private static (PropositionSet Set, BindingScope Scope, InMemoryPropositionStore Store) NewSet()
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult)
            .Register("customer.passes-check", PassesCheck);
        var scope = new BindingScope(registry);
        var store = new InMemoryPropositionStore();
        var set = new PropositionSet(scope, store).AddModel<Customer>("customer");
        return (set, scope, store);
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

        public IRebindCommit? PrepareRebind(ISpecSource prospective, ScopeGeneration world, List<RuleError> errors)
        {
            errors.Add(new RuleError("$", RuleErrorCode.AsyncSpecInSyncLoad, "would not bind"));
            return null;
        }
    }

    /// <summary>
    /// A participant whose <see cref="PrepareRebind"/> signals that it has been entered, then blocks
    /// until the test releases it — so a test can park a publish mid-closure-walk and observe what is
    /// excluded while it sits there.
    /// </summary>
    private sealed class BlockingParticipant(NodeId node) : IRebindable, IRebindCommit
    {
        private readonly ManualResetEventSlim _entered = new(initialState: false);
        private readonly ManualResetEventSlim _release = new(initialState: false);

        public NodeId Node { get; } = node;

        public IRebindCommit? PrepareRebind(ISpecSource prospective, ScopeGeneration world, List<RuleError> errors)
        {
            _entered.Set();
            _release.Wait();
            return this;
        }

        public void ApplyTo(ScopeGenerationBuilder builder)
        {
        }

        /// <summary>Blocks until <see cref="PrepareRebind"/> has been entered.</summary>
        public void WaitUntilEntered() => _entered.Wait();

        /// <summary>Lets a blocked <see cref="PrepareRebind"/> return.</summary>
        public void Release() => _release.Set();
    }

    [Fact]
    public async Task Should_bump_the_version_of_an_updated_document()
    {
        // Arrange
        var (set, _, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Act
        var result = await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        result.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_make_a_dependent_see_the_new_definition_without_touching_it()
    {
        // Arrange — this is the feature's central claim: b is never re-saved, yet its meaning follows a
        var (set, scope, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await set.CreateAsync("customer.b", "customer", """{ "rule": { "spec": "customer.a" } }""", null);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);
        Evaluate(scope, "customer.b", inactiveAdult).ShouldBeFalse();

        // Act — a now means "is an adult" instead of "is active"
        await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert
        Evaluate(scope, "customer.b", inactiveAdult).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_leave_a_dependents_version_alone_when_it_is_rebound()
    {
        // Arrange — bumping it would invalidate every colleague's open draft on an unrelated edit
        var (set, _, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await set.CreateAsync("customer.b", "customer", """{ "rule": { "spec": "customer.a" } }""", null);

        // Act
        await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert
        set.Find("customer.b")!.Version.ShouldBe(1);
    }

    /// <summary>
    /// A regression guard for a rebind that reaches <see cref="PropositionSet"/> only through a
    /// cascaded closure — never published directly, only carried along because it depends on a. The
    /// overlay (<see cref="Evaluate"/>'s path, and every other test in this class) is always fresh:
    /// <c>ApplyTo</c> writes it. The public listing — <see cref="PropositionSet.Find"/> — reads a
    /// second, separate dictionary that only a live <c>Commit()</c> write reaches; before that write
    /// was restored, this assertion failed while every overlay-based assertion in this file kept
    /// passing, which is why the regression needs its own test rather than reuse of an existing one.
    /// </summary>
    [Fact]
    public async Task Should_update_a_cascaded_dependents_listed_asyncness()
    {
        // Arrange — b is bound synchronously, against a's original synchronous document
        var (set, scope, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await set.CreateAsync("customer.b", "customer", """{ "rule": { "not": { "spec": "customer.a" } } }""", null);

        // Act — a now resolves to an async spec, so b's cascaded rebind must make b async too
        await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.passes-check" } }""", 1);

        // Assert
        scope.Source.Find("customer.b")!.IsAsync.ShouldBeTrue();
        set.Find("customer.b")!.IsAsync.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_cascade_through_a_chain()
    {
        // Arrange — a <- b <- c, so editing a must reach c
        var (set, scope, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await set.CreateAsync("customer.b", "customer", """{ "rule": { "spec": "customer.a" } }""", null);
        await set.CreateAsync("customer.c", "customer", """{ "rule": { "spec": "customer.b" } }""", null);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);

        // Act
        await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert
        Evaluate(scope, "customer.c", inactiveAdult).ShouldBeTrue();
    }

    /// <summary>
    /// Publishing re-enrols the dependent as a rebind participant, so a later cascade rebinds it
    /// from the document it currently has. An enrol-if-absent that kept the *first* participant
    /// would rebind b from its superseded document — b would silently revert on someone else's edit.
    /// </summary>
    [Fact]
    public async Task Should_rebind_an_updated_dependent_from_its_current_document()
    {
        // Arrange — b is edited to mean "not a" before a itself is edited
        var (set, scope, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await set.CreateAsync("customer.b", "customer", """{ "rule": { "spec": "customer.a" } }""", null);
        (await set.UpdateAsync("customer.b", """{ "rule": { "not": { "spec": "customer.a" } } }""", 1))
            .Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);

        // Act — a now means "is an adult", which the adult satisfies
        await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert — b means "not a", so it must now be false. Rebinding b's superseded document
        // would make it a plain "a" again and report true.
        Evaluate(scope, "customer.b", inactiveAdult).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_reject_a_stale_expected_version()
    {
        // Arrange
        var (set, _, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Act — a second editor still holding version 1
        var result = await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-active" } }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.VersionConflict);
        result.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_report_not_found_for_a_name_with_no_authored_document()
    {
        // Arrange — a compiled spec must be overridden via Create before it can be updated
        var (set, _, _) = NewSet();

        // Act
        var result = await set.UpdateAsync("customer.is-active", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.NotFound);
    }

    [Fact]
    public async Task Should_reject_an_update_that_would_close_a_cycle()
    {
        // Arrange — b references a; pointing a at b closes the loop
        var (set, _, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await set.CreateAsync("customer.b", "customer", """{ "rule": { "spec": "customer.a" } }""", null);

        // Act
        var result = await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.b" } }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.CycleDetected);
    }

    [Fact]
    public async Task Should_reject_the_whole_update_when_a_dependent_would_break()
    {
        // Arrange — a stubbed dependent stands in for a sync rule that cannot bind the new definition
        var (set, scope, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        scope.Locked(() =>
        {
            scope.Enrol(new AlwaysBreaks(NodeId.Rule("can-checkout")));
            scope.Mutate(builder => builder.Graph.Set(NodeId.Rule("can-checkout"), ["customer.a"]));
            return 0;
        });

        // Act
        var result = await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.BrokenDependents.Count.ShouldBe(1);
        result.BrokenDependents[0].Name.ShouldBe("can-checkout");
        result.BrokenDependents[0].Kind.ShouldBe("rule");
    }

    [Fact]
    public async Task Should_leave_everything_untouched_when_a_dependent_would_break()
    {
        // Arrange
        var (set, scope, store) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        scope.Locked(() =>
        {
            scope.Enrol(new AlwaysBreaks(NodeId.Rule("can-checkout")));
            scope.Mutate(builder => builder.Graph.Set(NodeId.Rule("can-checkout"), ["customer.a"]));
            return 0;
        });
        var inactiveAdult = new Customer(IsActive: false, Age: 30);

        // Act
        await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert — version, binding and persisted document all unmoved
        set.Find("customer.a")!.Version.ShouldBe(1);
        Evaluate(scope, "customer.a", inactiveAdult).ShouldBeFalse();
        store.Load()[0].DocumentJson.ShouldContain("customer.is-active");
    }

    /// <summary>
    /// Pins the inner monitor tier that <c>PersistAndCommitCoreAsync</c>'s prepare phase is supposed to
    /// hold — see the remarks on <c>RuleSet.PersistAndCommitCoreAsync</c>, whose shape this method's
    /// <c>PropositionSet</c> counterpart mirrors. A synchronous read that also takes only the monitor
    /// (<see cref="PropositionSet.Find"/>) must not be able to return while a publish is stalled inside
    /// the closure walk the monitor is meant to guard. Nothing else covers this: deleting the
    /// <c>Scope.Locked</c> wrapping in <c>PropositionSet.PersistAndCommitCoreAsync</c> leaves the rest
    /// of the suite green, and only this test red.
    /// </summary>
    [Fact]
    public async Task Should_hold_the_inner_monitor_across_a_publishs_prepare_phase()
    {
        // Arrange — "blocker" depends on "customer.a", so updating "a" must walk into "blocker"'s
        // PrepareRebind as part of the cascade, which is where this stalls the writer deliberately.
        var (set, scope, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        var blocker = new BlockingParticipant(NodeId.Rule("blocker"));
        scope.Locked(() =>
        {
            scope.Enrol(blocker);
            scope.Mutate(builder => builder.Graph.Set(NodeId.Rule("blocker"), ["customer.a"]));
            return 0;
        });

        // Act — start the update on a background thread: PrepareRebind blocks synchronously, so the
        // calling thread must not be the test thread or the whole test would hang.
        var updating = Task.Run(() =>
            set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-adult" } }""", 1));
        blocker.WaitUntilEntered();

        // The updating thread is now parked inside Scope.Locked's prepare phase, holding the inner
        // monitor. A concurrent Find, which takes only that same monitor, must not be able to
        // interleave with it — so it must still be running after a comfortably-short wait. Recorded
        // rather than asserted here so that a regression fails the test instead of leaving the stalled
        // writer parked for the rest of the run.
        var finding = Task.Run(() => set.Find("customer.a"));
        await Task.Delay(100);
        var readInterleaved = finding.IsCompleted;

        // Assert — releasing the writer lets the blocked read through, and the write itself succeeds
        blocker.Release();
        await finding;
        var result = await updating;

        readInterleaved.ShouldBeFalse();
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
    }

    [Fact]
    public async Task Should_revert_an_override_to_the_compiled_spec()
    {
        // Arrange — override is-active with something that inverts it
        var (set, scope, store) = NewSet();
        await set.CreateAsync("customer.is-active", "customer", """{ "rule": { "not": { "spec": "customer.is-adult" } } }""", null);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);
        Evaluate(scope, "customer.is-active", inactiveAdult).ShouldBeFalse();

        // Act
        var result = await set.WithdrawAsync("customer.is-active", 1);

        // Assert — the compiled spec, never copied or moved, resolves again
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
        set.Find("customer.is-active")!.Origin.ShouldBe(PropositionOrigin.Compiled);
        store.Load().ShouldBeEmpty();
        Evaluate(scope, "customer.is-active", new Customer(IsActive: true, Age: 30)).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_allow_reverting_an_override_that_others_reference()
    {
        // Arrange — referrers keep resolving, to the compiled spec beneath
        var (set, scope, _) = NewSet();
        await set.CreateAsync("customer.is-active", "customer", """{ "rule": { "not": { "spec": "customer.is-adult" } } }""", null);
        await set.CreateAsync("customer.b", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Act
        var result = await set.WithdrawAsync("customer.is-active", 1);

        // Assert — compiled "is active" says true; the withdrawn override (not is-adult) says false,
        // so this only passes if b genuinely rebound to the compiled spec
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
        Evaluate(scope, "customer.b", new Customer(IsActive: true, Age: 30)).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_reject_a_revert_that_would_break_a_dependent()
    {
        // Arrange — reverting to the compiled spec is still an edit, so it takes the same
        // transactional check: the compiled spec may not satisfy every dependent.
        var (set, scope, _) = NewSet();
        await set.CreateAsync("customer.is-active", "customer", """{ "rule": { "not": { "spec": "customer.is-adult" } } }""", null);
        scope.Locked(() =>
        {
            scope.Enrol(new AlwaysBreaks(NodeId.Rule("can-checkout")));
            scope.Mutate(builder => builder.Graph.Set(NodeId.Rule("can-checkout"), ["customer.is-active"]));
            return 0;
        });

        // Act
        var result = await set.WithdrawAsync("customer.is-active", 1);

        // Assert — nothing withdrawn
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.BrokenDependents.Count.ShouldBe(1);
        set.Find("customer.is-active")!.Origin.ShouldBe(PropositionOrigin.Overridden);
    }

    [Fact]
    public async Task Should_refuse_to_remove_an_authored_proposition_others_reference()
    {
        // Arrange — nothing lies beneath, so removal would leave b dangling
        var (set, _, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await set.CreateAsync("customer.b", "customer", """{ "rule": { "spec": "customer.a" } }""", null);

        // Act
        var result = await set.WithdrawAsync("customer.a", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Referenced);
        result.Referrers.ShouldBe(["customer.b"]);
    }

    [Fact]
    public async Task Should_remove_an_unreferenced_authored_proposition()
    {
        // Arrange
        var (set, scope, store) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);

        // Act
        var result = await set.WithdrawAsync("customer.a", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
        set.Find("customer.a").ShouldBeNull();
        scope.Source.Find("customer.a").ShouldBeNull();
        store.Load().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_reject_withdrawing_with_a_stale_version()
    {
        // Arrange
        var (set, _, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Act
        var result = await set.WithdrawAsync("customer.a", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.VersionConflict);
        result.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_report_not_found_when_withdrawing_a_purely_compiled_spec()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = await set.WithdrawAsync("customer.is-active", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.NotFound);
    }

    [Fact]
    public async Task Should_stop_rebinding_a_removed_proposition()
    {
        // Arrange — a stale participant rebinding after removal would resurrect it
        var (set, scope, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await set.CreateAsync("customer.b", "customer", """{ "rule": { "spec": "customer.a" } }""", null);
        await set.WithdrawAsync("customer.b", 1);

        // Act
        var result = await set.UpdateAsync("customer.a", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        scope.Source.Find("customer.b").ShouldBeNull();
    }

    [Fact]
    public async Task Should_report_the_transitive_dependents_of_a_proposition()
    {
        // Arrange — a <- b <- c, for the UI's blast-radius strip
        var (set, _, _) = NewSet();
        await set.CreateAsync("customer.a", "customer", """{ "rule": { "spec": "customer.is-active" } }""", null);
        await set.CreateAsync("customer.b", "customer", """{ "rule": { "spec": "customer.a" } }""", null);
        await set.CreateAsync("customer.c", "customer", """{ "rule": { "spec": "customer.b" } }""", null);

        // Act
        var dependents = set.Dependents("customer.a");

        // Assert
        dependents.Select(dependent => dependent.Name).ShouldBe(["customer.b", "customer.c"]);
        dependents.ShouldAllBe(dependent => dependent.Kind == "proposition");
    }

    private sealed record Customer(bool IsActive, int Age);
}
