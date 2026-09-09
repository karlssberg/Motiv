using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

public class StoreImportTests
{
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static StoredRuleVersion Row(string name, int version, string? documentJson = "{}") =>
        new(name, version, documentJson, "alice", Epoch, null, null, "build-7");

    private static StoredProposition Proposition(string name) =>
        new(name, "customer", """{ "rule": { "spec": "is-active" } }""", 1, null);

    [Fact]
    public async Task Should_carry_every_version_across_not_just_the_head()
    {
        // Arrange — the audit trail is the point: a head-only import would claim the rule was
        // authored at import time, which is exactly what an approval gate cannot tolerate
        var sourceRules = new InMemoryRuleStore();
        await sourceRules.AppendAsync([Row("a", 1, """{"v":1}""")], default);
        await sourceRules.AppendAsync([Row("a", 2, """{"v":2}""")], default);
        await sourceRules.AppendAsync([Row("a", 3, documentJson: null)], default);

        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var targetRules = new EfRuleStore(fixture.Factory);

        // Act
        var result = await StoreImport.CopyAsync(
            sourceRules, targetRules,
            new InMemoryPropositionStore(), new EfPropositionStore(fixture.Factory), default);

        // Assert
        result.Imported.ShouldBeTrue();
        result.RuleVersions.ShouldBe(3);

        var history = await targetRules.HistoryAsync("a", default);
        history.Select(row => row.Version).ShouldBe([1, 2, 3]);
        history[2].DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task Should_preserve_authorship_and_timestamps()
    {
        // Arrange — a copy that restamped these would produce a truthful-looking but false record
        var sourceRules = new InMemoryRuleStore();
        await sourceRules.AppendAsync(
            [new StoredRuleVersion("a", 1, "{}", "bob", Epoch, "why", "cr-9", "build-3")], default);

        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var targetRules = new EfRuleStore(fixture.Factory);

        // Act
        await StoreImport.CopyAsync(
            sourceRules, targetRules,
            new InMemoryPropositionStore(), new EfPropositionStore(fixture.Factory), default);

        // Assert
        var row = (await targetRules.HistoryAsync("a", default)).ShouldHaveSingleItem();
        row.Author.ShouldBe("bob");
        row.TimestampUtc.ShouldBe(Epoch);
        row.ChangeNote!.ShouldBe("why");
        row.ApprovalRef!.ShouldBe("cr-9");
        row.BuildId!.ShouldBe("build-3");
    }

    [Fact]
    public async Task Should_copy_propositions_too()
    {
        // Arrange
        var sourcePropositions = new InMemoryPropositionStore();
        await sourcePropositions.WriteAsync(
            PropositionBatch.Save(Proposition("customer.is-eligible")), default);

        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var targetPropositions = new EfPropositionStore(fixture.Factory);

        // Act
        var result = await StoreImport.CopyAsync(
            new InMemoryRuleStore(), new EfRuleStore(fixture.Factory),
            sourcePropositions, targetPropositions, default);

        // Assert
        result.Propositions.ShouldBe(1);
        targetPropositions.Load().ShouldHaveSingleItem().Name.ShouldBe("customer.is-eligible");
    }

    [Fact]
    public async Task Should_refuse_a_target_that_already_holds_rules()
    {
        // Arrange — refusing on a non-empty target is what makes a second run harmless, with no
        // import state to track anywhere
        var sourceRules = new InMemoryRuleStore();
        await sourceRules.AppendAsync([Row("a", 1)], default);

        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var targetRules = new EfRuleStore(fixture.Factory);
        await targetRules.AppendAsync([Row("existing", 1)], default);

        // Act
        var result = await StoreImport.CopyAsync(
            sourceRules, targetRules,
            new InMemoryPropositionStore(), new EfPropositionStore(fixture.Factory), default);

        // Assert — nothing copied, nothing thrown
        result.Imported.ShouldBeFalse();
        result.RuleVersions.ShouldBe(0);
        (await targetRules.HistoryAsync("a", default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_report_nothing_imported_when_the_source_is_empty()
    {
        // Arrange
        await using var fixture = await SqliteStoreFixture.CreateAsync();

        // Act
        var result = await StoreImport.CopyAsync(
            new InMemoryRuleStore(), new EfRuleStore(fixture.Factory),
            new InMemoryPropositionStore(), new EfPropositionStore(fixture.Factory), default);

        // Assert
        result.Imported.ShouldBeTrue();
        result.RuleVersions.ShouldBe(0);
        result.Propositions.ShouldBe(0);
    }

    [Fact]
    public async Task Should_copy_the_propositions_before_the_rules()
    {
        // Arrange — the refuse-check reads the rules first, so the rules must be the last thing to
        // become non-empty: a crash between the two sides then leaves a target whose rule side is
        // still empty
        var sourceRules = new InMemoryRuleStore();
        await sourceRules.AppendAsync([Row("a", 1)], default);

        var sourcePropositions = new InMemoryPropositionStore();
        await sourcePropositions.WriteAsync(PropositionBatch.Save(Proposition("p")), default);

        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var targetPropositions = new EfPropositionStore(fixture.Factory);
        var targetRules = new FailingRuleStore(new EfRuleStore(fixture.Factory), failOnAppend: 1);

        // Act
        var act = async () => await StoreImport.CopyAsync(
            sourceRules, targetRules, sourcePropositions, targetPropositions, default);

        // Assert — the propositions had already landed when the rule side blew up. This is the
        // propositionsWritten > 0 side of the catch's guard, distinct from the ruleVersions > 0
        // side that "Should_throw_naming_the_partial_state_when_a_write_fails_mid_import" below
        // exercises — the guard is an OR, and both operands need a true case to be fully covered.
        var thrown = await act.ShouldThrowAsync<InvalidOperationException>();
        thrown.Message.ShouldContain("PARTIALLY imported");
        thrown.Message.ShouldContain("1 proposition(s)");
        thrown.Message.ShouldContain("0 rule version row(s)");
        thrown.InnerException.ShouldBeOfType<ImportFailure>();
        targetPropositions.Load().ShouldHaveSingleItem().Name.ShouldBe("p");
    }

    [Fact]
    public async Task Should_wrap_a_mid_import_conflict_naming_the_conflicting_rule()
    {
        // Arrange — 'a' imports cleanly, then 'b' is reported as an outright conflict, as if some
        // other writer already holds that (Name, Version) even though the target was supposedly
        // empty when the import began. That throw (a few lines above the catch this file's other
        // tests exercise) is itself caught by the same catch and re-wrapped, so the outer message
        // must still say PARTIALLY imported while the inner one keeps naming the actual rule.
        var sourceRules = new InMemoryRuleStore();
        await sourceRules.AppendAsync([Row("a", 1)], default);
        await sourceRules.AppendAsync([Row("b", 1)], default);

        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var targetRules = new ConflictingRuleStore(new EfRuleStore(fixture.Factory), conflictOnAppend: 2);

        // Act
        var act = async () => await StoreImport.CopyAsync(
            sourceRules, targetRules,
            new InMemoryPropositionStore(), new EfPropositionStore(fixture.Factory), default);

        // Assert — the outer exception is the usual partial-import wrapper, and its inner
        // exception is the conflict throw, naming the rule and version it conflicted at
        var thrown = await act.ShouldThrowAsync<InvalidOperationException>();
        thrown.Message.ShouldContain("PARTIALLY imported");
        thrown.Message.ShouldContain("1 rule version row(s)");
        thrown.InnerException.ShouldNotBeNull();
        thrown.InnerException!.Message.ShouldContain("Import of rule 'b' conflicted at version 5");
    }

    [Fact]
    public async Task Should_throw_naming_the_partial_state_when_a_write_fails_mid_import()
    {
        // Arrange — 'a' imports, 'b' fails: the target now holds part of the import, and every
        // later run would be refused and report Imported: false, which reads exactly like the
        // benign already-done case
        var sourceRules = new InMemoryRuleStore();
        await sourceRules.AppendAsync([Row("a", 1)], default);
        await sourceRules.AppendAsync([Row("b", 1)], default);

        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var targetRules = new FailingRuleStore(new EfRuleStore(fixture.Factory), failOnAppend: 2);

        // Act
        var act = async () => await StoreImport.CopyAsync(
            sourceRules, targetRules,
            new InMemoryPropositionStore(), new EfPropositionStore(fixture.Factory), default);

        // Assert — loud, specific, and it says what to do about it
        var thrown = await act.ShouldThrowAsync<InvalidOperationException>();
        thrown.Message.ShouldContain("PARTIALLY imported");
        thrown.Message.ShouldContain("Empty the target");
        thrown.InnerException.ShouldBeOfType<ImportFailure>();

        // And the claim in that message is true: a rerun is a silent no-op, not a repair
        var rerun = await StoreImport.CopyAsync(
            sourceRules, new EfRuleStore(fixture.Factory),
            new InMemoryPropositionStore(), new EfPropositionStore(fixture.Factory), default);
        rerun.Imported.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_let_a_failure_before_the_first_write_out_unwrapped()
    {
        // Arrange — nothing has been mutated yet, so this is not a partial import and must not be
        // dressed up as one: a source that cannot be read is the caller's own exception
        var sourceRules = new FailingRuleStore(new InMemoryRuleStore(), failOnAppend: 0)
        {
            FailOnHistory = true
        };
        await sourceRules.Inner.AppendAsync([Row("a", 1)], default);

        await using var fixture = await SqliteStoreFixture.CreateAsync();

        // Act
        var act = async () => await StoreImport.CopyAsync(
            sourceRules, new EfRuleStore(fixture.Factory),
            new InMemoryPropositionStore(), new EfPropositionStore(fixture.Factory), default);

        // Assert
        await act.ShouldThrowAsync<ImportFailure>();
    }
    // ---- Cancellation ----------------------------------------------------------------------
    //
    // Every case below captures with xUnit's Record.ExceptionAsync rather than Shouldly's
    // ShouldThrowAsync, and that is load-bearing rather than stylistic. An async method that throws
    // an OperationCanceledException does not return a *faulted* task: AsyncTaskMethodBuilder
    // .SetException special-cases cancellation into TrySetCanceled, so the task enters the Canceled
    // state. Record.ExceptionAsync rethrows the stored instance; ShouldThrowAsync hands back a
    // freshly constructed TaskCanceledException — destroying the identity, the derived type and the
    // Data that these tests exist to assert on. Assert through it and none of them can see their own
    // subject. Shouldly is still used for the assertions themselves; only the capture differs.

    /// <summary>
    /// Imports into a fresh target that is interrupted part-way through by <paramref name="failure"/>,
    /// and returns what reached the caller.
    /// </summary>
    /// <remarks>
    /// With <paramref name="withPropositions"/> the propositions land before the rule side is
    /// reached, so the import really is partial when it stops — that is what puts the wrapper's
    /// guard into its true branch. Without them, the interrupted append is the first write
    /// attempted and the target is still empty.
    /// </remarks>
    private static async Task<Exception> ImportInterruptedByAsync(
        SqliteStoreFixture fixture,
        Func<Exception> failure,
        CancellationToken cancellationToken = default,
        bool withPropositions = true)
    {
        var sourceRules = new InMemoryRuleStore();
        await sourceRules.AppendAsync([Row("a", 1)], default);

        var sourcePropositions = new InMemoryPropositionStore();
        if (withPropositions)
            await sourcePropositions.WriteAsync(PropositionBatch.Save(Proposition("p")), default);

        var targetRules = new FailingRuleStore(new EfRuleStore(fixture.Factory), failOnAppend: 1)
        {
            AppendFailure = failure
        };

        // ShouldNotBeNull both unwraps the nullable and turns "the import did not throw at all" —
        // which would make every assertion below vacuous — into a failure that says so.
        var thrown = await Record.ExceptionAsync(async () => await StoreImport.CopyAsync(
            sourceRules, targetRules, sourcePropositions, new EfPropositionStore(fixture.Factory),
            cancellationToken));

        return thrown.ShouldNotBeNull();
    }

    /// <summary>
    /// Cancels <paramref name="cancellation"/> and then reports it, from inside the append.
    /// </summary>
    /// <remarks>
    /// Cancelling here rather than before the call is what makes these tests mean anything.
    /// <c>CopyAsync</c>'s first act is to read the target, so a token already cancelled on entry
    /// would throw out there — outside the try — and a test asserting that cancellation is no longer
    /// swallowed would pass against the unfixed code, having never reached the catch it is about.
    /// It is also what really happens: the token trips while a write is in flight.
    /// </remarks>
    private static Func<Exception> CancelsDuringTheAppend(CancellationTokenSource cancellation) =>
        () =>
        {
            cancellation.Cancel();
            return new OperationCanceledException(cancellation.Token);
        };

    [Fact]
    public async Task Should_let_a_cancellation_out_as_itself_rather_than_the_partial_import_wrapper()
    {
        // Arrange — a cancellation is not a failure report, it is the caller's own request coming
        // back to them. Wrapping it in InvalidOperationException means a caller who cancels for a
        // graceful shutdown never sees their catch (OperationCanceledException) fire, and instead
        // handles their own shutdown as an unexpected error.
        using var cancellation = new CancellationTokenSource();
        await using var fixture = await SqliteStoreFixture.CreateAsync();

        // Act — the propositions have landed by the time the append is reached, so the wrapper's
        // guard is in its true branch and the only thing keeping this exception intact is the
        // cancellation exclusion itself
        var thrown = await ImportInterruptedByAsync(
            fixture, CancelsDuringTheAppend(cancellation), cancellation.Token);

        // Assert — an OperationCanceledException, which is already the whole claim: the wrapper the
        // defect produced is an InvalidOperationException, outside this hierarchy entirely
        var cancelled = thrown.ShouldBeAssignableTo<OperationCanceledException>();
        cancelled.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task Should_keep_the_derived_cancellation_type_a_caller_may_be_catching()
    {
        // Arrange — TaskCanceledException derives from OperationCanceledException, so a fix that
        // rethrew a freshly constructed OperationCanceledException carrying the partial-state
        // message would flatten this away: the same defect as the one being fixed, one level down.
        // Whatever channel surfaces that message must not touch the exception's identity.
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var cancelled = new TaskCanceledException();

        // Act
        var thrown = await ImportInterruptedByAsync(fixture, () => cancelled);

        // Assert — the very same instance, not a reconstruction of it
        thrown.ShouldBeSameAs(cancelled);
    }

    [Fact]
    public async Task Should_describe_the_partial_state_on_the_cancellation_it_lets_through()
    {
        // Arrange — letting cancellation through must not mean letting it through *silently*. A
        // cancelled import has left the target partially written just as surely as a failed one
        // has, and every later run will be refused and reported as nothing-to-import. The caller
        // knows they cancelled; what they cannot know without being told is that the target now
        // needs emptying before the import can ever succeed again.
        using var cancellation = new CancellationTokenSource();
        await using var fixture = await SqliteStoreFixture.CreateAsync();

        // Act
        var thrown = await ImportInterruptedByAsync(
            fixture, CancelsDuringTheAppend(cancellation), cancellation.Token);

        // Assert — the same sentence the wrapper would have carried, on a documented key
        thrown.ShouldBeAssignableTo<OperationCanceledException>();
        var described = thrown.Data[StoreImport.PartialImportDataKey].ShouldBeOfType<string>();
        described.ShouldContain("PARTIALLY imported");
        described.ShouldContain("1 proposition(s)");
        described.ShouldContain("0 rule version row(s)");
        described.ShouldContain("Empty the target");
    }

    [Fact]
    public async Task Should_not_describe_a_partial_state_on_a_cancellation_that_wrote_nothing()
    {
        // Arrange — with no propositions to copy, the cancelled append is the first write attempted:
        // the target is still empty and a retry is still clean. Marking that exception as a partial
        // import would send a caller to drop and recreate tables that hold nothing.
        using var cancellation = new CancellationTokenSource();
        await using var fixture = await SqliteStoreFixture.CreateAsync();

        // Act
        var thrown = await ImportInterruptedByAsync(
            fixture, CancelsDuringTheAppend(cancellation), cancellation.Token,
            withPropositions: false);

        // Assert
        thrown.ShouldBeAssignableTo<OperationCanceledException>();
        thrown.Data[StoreImport.PartialImportDataKey].ShouldBeNull();
    }

    [Fact]
    public async Task Should_still_let_the_cancellation_out_when_its_Data_refuses_the_description()
    {
        // Arrange — Exception.Data is a virtual property, so a consumer's own cancellation type may
        // return one that refuses writes. Attaching the description is a courtesy; letting the
        // caller's cancellation reach them is the contract, and the courtesy must never cost the
        // contract. Failing here would replace their cancellation with a NotSupportedException —
        // #130's defect again, wearing a different exception type.
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var cancelled = new UnwritableDataCancellation();

        // Act
        var thrown = await ImportInterruptedByAsync(fixture, () => cancelled);

        // Assert — the caller's own exception, undisturbed
        thrown.ShouldBeSameAs(cancelled);
    }
}

/// <summary>
/// A cancellation whose <see cref="Exception.Data"/> refuses writes, as a consumer's own
/// <see cref="OperationCanceledException"/> subclass is free to be. Attaching the partial-state
/// description must not be able to replace the caller's cancellation with a
/// <see cref="NotSupportedException"/> — that would be this ticket's defect wearing a different
/// exception type.
/// </summary>
public sealed class UnwritableDataCancellation : OperationCanceledException
{
    public override System.Collections.IDictionary Data { get; } =
        new System.Collections.Generic.Dictionary<string, string?>().AsReadOnly();
}

/// <summary>The failure an import test injects, distinguishable from anything the code throws.</summary>
public sealed class ImportFailure() : Exception("injected");

/// <summary>
/// A store that fails on demand: on the nth <see cref="AppendAsync"/>, or on any history read.
/// Everything else forwards, so the target really is left in whatever state the failure implies.
/// History failures are always <see cref="ImportFailure"/>; only the append path is configurable,
/// because that is the only one any test needs to vary.
/// </summary>
public sealed class FailingRuleStore(IRuleStore inner, int failOnAppend) : IRuleStore
{
    private int _appends;

    public IRuleStore Inner { get; } = inner;

    public bool FailOnHistory { get; init; }

    /// <summary>
    /// What the nth append throws. Defaults to <see cref="ImportFailure"/>; a cancellation test
    /// substitutes a cancellation exception, which the import must treat differently.
    /// </summary>
    /// <remarks>
    /// Invoked <em>at</em> the failure point rather than when the store is built, so a test may use
    /// it to arrange state first — the cancellation tests cancel their token from inside it, which
    /// is the only way to have the token trip mid-import rather than before the call.
    /// </remarks>
    public Func<Exception> AppendFailure { get; init; } = static () => new ImportFailure();

    public IReadOnlyList<StoredRule> Load() => Inner.Load();

    public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken) =>
        Inner.LoadAsync(cancellationToken);

    public Task<long> GetGenerationAsync(CancellationToken cancellationToken) =>
        Inner.GetGenerationAsync(cancellationToken);

    public Task<RuleAppendResult> AppendAsync(
        IReadOnlyList<StoredRuleVersion> versions, CancellationToken cancellationToken) =>
        ++_appends == failOnAppend
            ? throw AppendFailure()
            : Inner.AppendAsync(versions, cancellationToken);

    public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken) =>
        FailOnHistory ? throw new ImportFailure() : Inner.HistoryAsync(name, cancellationToken);
}

/// <summary>
/// A store whose nth <see cref="AppendAsync"/> reports a version conflict instead of writing, as
/// if a second writer had reached the target at the same time — a value returned, not a thrown
/// exception, which is a different way for <see cref="StoreImport.CopyAsync"/> to fail mid-import.
/// </summary>
public sealed class ConflictingRuleStore(IRuleStore inner, int conflictOnAppend) : IRuleStore
{
    private int _appends;

    public IReadOnlyList<StoredRule> Load() => inner.Load();

    public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken) =>
        inner.LoadAsync(cancellationToken);

    public Task<long> GetGenerationAsync(CancellationToken cancellationToken) =>
        inner.GetGenerationAsync(cancellationToken);

    public Task<RuleAppendResult> AppendAsync(
        IReadOnlyList<StoredRuleVersion> versions, CancellationToken cancellationToken) =>
        ++_appends == conflictOnAppend
            ? Task.FromResult(RuleAppendResult.Conflict("b", 5))
            : inner.AppendAsync(versions, cancellationToken);

    public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken) =>
        inner.HistoryAsync(name, cancellationToken);
}
