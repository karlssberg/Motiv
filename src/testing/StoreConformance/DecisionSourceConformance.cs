using System;
using System.Threading.Tasks;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Testing;

/// <summary>What it means to read the decision log back: by id, and by the existing query.</summary>
public abstract class DecisionSourceConformance : IAsyncLifetime
{
    protected IDecisionSink Sink { get; private set; } = null!;
    protected IDecisionSource Source { get; private set; } = null!;

    /// <summary>One object that is both the sink written to and the source read from.</summary>
    protected abstract Task<(IDecisionSink Sink, IDecisionSource Source)> CreateAsync();

    protected virtual Task DisposeStoreAsync() => Task.CompletedTask;

    public async Task InitializeAsync() => (Sink, Source) = await CreateAsync();

    public Task DisposeAsync() => DisposeStoreAsync();

    protected static DecisionRecord Record(
        string rule = "can-checkout", bool satisfied = true, string? correlation = null, DateTimeOffset? at = null) =>
        new(Guid.NewGuid(), correlation ?? Guid.NewGuid().ToString("N"), at ?? DateTimeOffset.UtcNow, "alice",
            rule, RuleVersion: 3, BuildId: "build-1", [new PropositionVersion("customer.is-active", 2)],
            DecisionInput.Reference("cust-42"),
            new RuleEvaluationResult<object?>(satisfied, "r", ["r"], [], "r", new ExplanationNode(["r"], [])));

    [Fact]
    public async Task Should_find_a_written_record_by_id()
    {
        var record = Record();
        await Sink.WriteAsync([record], default);

        var found = await Source.FindAsync(record.Id, default);

        found.ShouldNotBeNull();
        found.RuleName.ShouldBe("can-checkout");
        found.RuleVersion.ShouldBe(3);
        found.ReferencedPropositionVersions.ShouldBe([new PropositionVersion("customer.is-active", 2)]);
        found.Input!.Kind.ShouldBe(DecisionInputKind.Reference);
        found.Input.Value.ShouldBe("cust-42");
    }

    [Fact]
    public async Task Should_answer_null_for_an_unknown_id()
    {
        (await Source.FindAsync(Guid.NewGuid(), default)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_query_by_rule_newest_first_and_capped()
    {
        var t = DateTimeOffset.UtcNow;
        await Sink.WriteAsync([Record("a", at: t.AddSeconds(-2)), Record("a", at: t.AddSeconds(-1)), Record("b", at: t)], default);

        var rows = await Source.QueryAsync(new DecisionQuery { RuleName = "a", Limit = 1 }, default);

        rows.ShouldHaveSingleItem().TimestampUtc.ShouldBe(t.AddSeconds(-1), TimeSpan.FromMilliseconds(1));
    }
}
