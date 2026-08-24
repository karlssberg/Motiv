namespace Motiv.Serialization.Tests.Decisions;

/// <summary>
/// The record and the sink contract, in isolation from the evaluation that produces them.
/// </summary>
public class DecisionRecordTests
{
    private static readonly DateTimeOffset AnInstant = new(2026, 8, 24, 14, 7, 0, TimeSpan.Zero);

    private static RuleEvaluationResult<object?> AnOutcome() =>
        new(true, "is-active == true", ["is-active == true"], ["is-active == true"],
            "is-active == true", new ExplanationNode(["is-active == true"], []));

    private static DecisionRecord ARecord(string ruleName = "can-checkout", string? correlationId = null) =>
        new(
            Id: Guid.NewGuid(),
            CorrelationId: correlationId ?? Guid.NewGuid().ToString("N"),
            TimestampUtc: AnInstant,
            Caller: "alice",
            RuleName: ruleName,
            RuleVersion: 3,
            BuildId: "1.2.3+abcdef",
            ReferencedPropositionVersions: [new PropositionVersion("customer.is-active", 7)],
            Input: DecisionInput.Reference("cust-42"),
            Outcome: AnOutcome());

    [Fact]
    public void Should_carry_the_three_behaviour_anchors()
    {
        // Act
        var record = ARecord();

        // Assert — the document's version, the build, and what the names it resolved through meant
        record.RuleVersion.ShouldBe(3);
        record.BuildId.ShouldBe("1.2.3+abcdef");
        record.ReferencedPropositionVersions.ShouldHaveSingleItem()
            .ShouldBe(new PropositionVersion("customer.is-active", 7));
    }

    [Fact]
    public void Should_compare_proposition_versions_by_value()
    {
        // Assert — the anchor list is compared in tests and by adopters; reference equality would
        // make every such comparison silently false
        new PropositionVersion("a", 1).ShouldBe(new PropositionVersion("a", 1));
        new PropositionVersion("a", 1).ShouldNotBe(new PropositionVersion("a", 2));
    }

    [Theory]
    [InlineData(DecisionInputKind.Whole)]
    [InlineData(DecisionInputKind.Redacted)]
    [InlineData(DecisionInputKind.Reference)]
    public void Should_name_the_capture_posture_that_produced_the_input(DecisionInputKind kind)
    {
        // Act
        var input = kind switch
        {
            DecisionInputKind.Whole => DecisionInput.Whole(new { Id = "cust-42" }),
            DecisionInputKind.Redacted => DecisionInput.Redacted(new { Id = "cust-42" }),
            _ => DecisionInput.Reference("cust-42")
        };

        // Assert — a reader must be able to tell what a record's input is worth for replay without
        // guessing from its shape
        input.Kind.ShouldBe(kind);
        input.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_accumulate_records_in_order_in_the_in_memory_sink()
    {
        // Arrange
        var sink = new InMemoryDecisionSink();

        // Act
        await sink.WriteAsync([ARecord("first"), ARecord("second")], CancellationToken.None);
        await sink.WriteAsync([ARecord("third")], CancellationToken.None);

        // Assert
        sink.Records.Select(r => r.RuleName).ShouldBe(["first", "second", "third"]);
    }

    [Fact]
    public async Task Should_keep_gaps_separate_from_records_in_the_in_memory_sink()
    {
        // Arrange
        var sink = new InMemoryDecisionSink();

        // Act
        await sink.WriteGapAsync(new DecisionGap(AnInstant, AnInstant.AddSeconds(2), 5), CancellationToken.None);
        await sink.WriteAsync([ARecord()], CancellationToken.None);

        // Assert — a gap is evidence about the log, not a decision, so it must not be countable as one
        sink.Records.ShouldHaveSingleItem();
        sink.Gaps.ShouldHaveSingleItem().DroppedCount.ShouldBe(5);
    }
}
