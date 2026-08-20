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

        // Assert — the propositions had already landed when the rule side blew up
        await act.ShouldThrowAsync<InvalidOperationException>();
        targetPropositions.Load().ShouldHaveSingleItem().Name.ShouldBe("p");
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
}

/// <summary>The failure an import test injects, distinguishable from anything the code throws.</summary>
public sealed class ImportFailure() : Exception("injected");

/// <summary>
/// A store that fails on demand: on the nth <see cref="AppendAsync"/>, or on any history read.
/// Everything else forwards, so the target really is left in whatever state the failure implies.
/// </summary>
public sealed class FailingRuleStore(IRuleStore inner, int failOnAppend) : IRuleStore
{
    private int _appends;

    public IRuleStore Inner { get; } = inner;

    public bool FailOnHistory { get; init; }

    public IReadOnlyList<StoredRule> Load() => Inner.Load();

    public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken) =>
        Inner.LoadAsync(cancellationToken);

    public Task<long> GetGenerationAsync(CancellationToken cancellationToken) =>
        Inner.GetGenerationAsync(cancellationToken);

    public Task<RuleAppendResult> AppendAsync(
        IReadOnlyList<StoredRuleVersion> versions, CancellationToken cancellationToken) =>
        ++_appends == failOnAppend
            ? throw new ImportFailure()
            : Inner.AppendAsync(versions, cancellationToken);

    public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken) =>
        FailOnHistory ? throw new ImportFailure() : Inner.HistoryAsync(name, cancellationToken);
}
