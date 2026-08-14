namespace Motiv.Serialization.Tests.Rules;

public class RuleVersionLogTests
{
    // Plain class (not a record) so the net472 target compiles without an IsExternalInit polyfill.
    private sealed class Customer(bool isActive)
    {
        public bool IsActive { get; } = isActive;
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class SampleRule() : Rule<Customer, string>("sample", IsActive);

    private const string V2 = """{ "rule": { "spec": "customer.is-active" } }""";
    private const string V3 = """{ "rule": { "not": { "spec": "customer.is-active" } } }""";

    // Built by hand rather than via DateTimeOffset.UnixEpoch — that static field is unavailable on
    // net472/netstandard2.0, two of this project's target frameworks.
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A store that blocks inside AppendAsync until released — a hung database.</summary>
    private sealed class StallingRuleStore : IRuleStore
    {
        private readonly InMemoryRuleStore _inner = new();
        public TaskCompletionSource<bool> Released { get; } = new();
        public TaskCompletionSource<bool> Entered { get; } = new();

        public IReadOnlyList<StoredRule> Load() => _inner.Load();
        public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken ct) => _inner.LoadAsync(ct);
        public Task<long> GetGenerationAsync(CancellationToken ct) => _inner.GetGenerationAsync(ct);
        public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string name, CancellationToken ct) =>
            _inner.HistoryAsync(name, ct);

        public async Task<RuleAppendResult> AppendAsync(
            IReadOnlyList<StoredRuleVersion> versions, CancellationToken ct)
        {
            Entered.TrySetResult(true);
            await Released.Task;
            return await _inner.AppendAsync(versions, ct);
        }
    }

    private static (RuleSet Set, IRuleStore Store) Fresh(IRuleStore? store = null)
    {
        store ??= new InMemoryRuleStore();
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var set = new RuleSet(registry, store).Add(new SampleRule());
        set.Load();
        return (set, store);
    }

    [Fact]
    public async Task Should_append_a_version_row_carrying_who_and_why()
    {
        // Arrange
        var (set, store) = Fresh();

        // Act
        var result = await set.UpdateAsync(
            "sample", V2, 1, new RuleChangeProvenance("alice", "tighten the check"));

        // Assert
        result.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        var history = await ((InMemoryRuleStore)store).HistoryAsync("sample", default);
        history.ShouldHaveSingleItem();
        history[0].Version.ShouldBe(2);
        history[0].DocumentJson!.ShouldBe(V2);
        history[0].Author.ShouldBe("alice");
        history[0].ChangeNote!.ShouldBe("tighten the check");
        history[0].BuildId!.ShouldBe(BuildIdentity.Current);
    }

    [Fact]
    public async Task Should_append_a_null_document_row_on_a_revert()
    {
        // Arrange
        var (set, store) = Fresh();
        await set.UpdateAsync("sample", V2, 1, new RuleChangeProvenance("alice"));

        // Act
        await set.RevertAsync("sample", 2, new RuleChangeProvenance("bob"));

        // Assert — the version moves forward and the row records the return to code
        var history = await store.HistoryAsync("sample", default);
        history.Select(row => row.Version).ShouldBe([2, 3]);
        history[1].DocumentJson.ShouldBeNull();
        history[1].Author.ShouldBe("bob");
    }

    [Fact]
    public async Task Should_leave_nothing_live_when_the_store_refuses_the_append()
    {
        // Arrange — a second replica already took version 2
        var (set, store) = Fresh();
        await store.AppendAsync([new StoredRuleVersion(
            "sample", 2, """{"other":"replica"}""", "carol",
            Epoch, null, null, "test")], default);

        // Act
        var result = await set.UpdateAsync("sample", V2, 1, new RuleChangeProvenance("alice"));

        // Assert — the PK is the compare-and-set; the loser is told the current version
        result.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        result.Version.ShouldBe(2);
        set.FindEntry("sample")!.Version.ShouldBe(1);
        set.FindEntry("sample")!.DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task Should_not_append_anything_when_the_document_does_not_bind()
    {
        // Arrange
        var (set, store) = Fresh();

        // Act
        var result = await set.UpdateAsync(
            "sample", """{ "rule": { "spec": "nope" } }""", 1, new RuleChangeProvenance("alice"));

        // Assert — everything fallible runs before anything mutates, in both directions
        result.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        store.Load().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_restore_an_old_version_by_appending_a_copy_of_it()
    {
        // Arrange
        var (set, store) = Fresh();
        await set.UpdateAsync("sample", V2, 1, new RuleChangeProvenance("alice"));
        await set.UpdateAsync("sample", V3, 2, new RuleChangeProvenance("alice"));

        // Act — roll back to v2
        var result = await set.RestoreAsync(
            "sample", targetVersion: 2, expectedVersion: 3,
            new RuleChangeProvenance("bob", "rollback"), default);

        // Assert — rollback appends: restoring v2 writes v4, which also records that it happened
        result.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        result.Version.ShouldBe(4);
        set.FindEntry("sample")!.DocumentJson!.ShouldBe(V2);

        var history = await store.HistoryAsync("sample", default);
        history.Select(row => row.Version).ShouldBe([2, 3, 4]);
        history[2].DocumentJson!.ShouldBe(V2);
        history[2].ChangeNote!.ShouldBe("rollback");
    }

    [Fact]
    public async Task Should_refuse_to_restore_a_version_that_was_never_recorded()
    {
        // Arrange
        var (set, _) = Fresh();
        await set.UpdateAsync("sample", V2, 1, new RuleChangeProvenance("alice"));

        // Act
        var result = await set.RestoreAsync("sample", 99, 2, new RuleChangeProvenance("bob"), default);

        // Assert
        result.Outcome.ShouldBe(RuleUpdateOutcome.NotFound);
    }

    [Fact]
    public async Task Should_survive_a_restart_with_the_document_and_version_intact()
    {
        // Arrange
        var (set, store) = Fresh();
        await set.UpdateAsync("sample", V2, 1, new RuleChangeProvenance("alice"));

        // Act — a fresh RuleSet over the same store is what a restart looks like
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var restarted = new RuleSet(registry, store).Add(new SampleRule());
        var report = restarted.Load();

        // Assert — the thing an enterprise governs now survives a restart
        report.Quarantined.ShouldBeEmpty();
        restarted.FindEntry("sample")!.DocumentJson!.ShouldBe(V2);
        restarted.FindEntry("sample")!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_cancel_a_write_waiting_behind_a_stuck_store()
    {
        // Arrange — the cancellation this whole async contract exists for
        var store = new StallingRuleStore();
        var (set, _) = Fresh(store);

        var stuck = set.UpdateAsync("sample", V2, 1, new RuleChangeProvenance("alice"));
        await store.Entered.Task;

        using var cancellation = new CancellationTokenSource();

        // Act
        var queued = set.UpdateAsync(
            "sample", V2, 1, new RuleChangeProvenance("bob"), cancellation.Token);
        cancellation.Cancel();

        // Assert — the second writer escapes rather than hanging forever
        await Should.ThrowAsync<OperationCanceledException>(async () => await queued);

        store.Released.SetResult(true);
        (await stuck).Outcome.ShouldBe(RuleUpdateOutcome.Updated);
    }

    // Should_serialise_two_concurrent_writers_into_one_winner was deleted: its assertion (one
    // Updated, one VersionConflict at version 2) is guaranteed by the store's own (Name, Version)
    // primary key, not by BindingScope's exclusion gate — confirmed by disabling the gate entirely
    // and observing the test still pass, even rewritten with a stalling store that forces the two
    // writes to genuinely overlap. There is no gate-dependent property this shape of assertion can
    // exercise; RuleTests.Should_report_a_version_conflict_for_a_stale_expected_version already
    // covers the store's version-conflict behaviour sequentially, which is the whole of what this
    // test could ever prove.
}
