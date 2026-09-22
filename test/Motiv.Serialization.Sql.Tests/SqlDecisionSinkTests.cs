using System.Text.Json;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Sql.Tests;

/// <summary>
/// The sink against a real database: what it writes, and what comes back. Behavioural conformance
/// runs on SQLite alone, which is sound because nothing in the write path inspects a provider error
/// code — what a SQLite-only suite leaves unproven is the SQL text, and
/// <see cref="DecisionSqlDialectTests"/> is what proves that.
/// </summary>
public class SqlDecisionSinkTests
{
    [Fact]
    public async Task Should_create_its_schema_and_say_so_twice()
    {
        // Arrange — the file does not exist yet
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();

        // Act — twice, because a replica restarting against a live table must not fail here
        await sink.EnsureSchemaAsync();
        await sink.EnsureSchemaAsync();

        // Assert — an empty read proves the tables exist; a missing table would throw
        (await sink.ReadAsync(new DecisionQuery())).ShouldBeEmpty();
        (await sink.ReadGapsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_bootstrap_the_schema_on_the_first_write()
    {
        // Arrange — nothing calls EnsureSchemaAsync, which is the zero-config path
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();

        // Act
        await sink.WriteAsync([Decisions.Record()], CancellationToken.None);

        // Assert
        (await sink.ReadAsync(new DecisionQuery())).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_round_trip_every_field_of_a_record()
    {
        // Arrange
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();
        var written = Decisions.Record(
            id: Guid.NewGuid(),
            correlationId: "trace-abc",
            timestampUtc: new DateTimeOffset(2026, 8, 25, 14, 7, 3, 456, TimeSpan.Zero),
            caller: "alice@example.com",
            ruleName: "checkout.can-checkout",
            ruleVersion: 12,
            buildId: "1.4.0+abcdef",
            referenced:
            [
                new PropositionVersion("customer.is-active", 3),
                new PropositionVersion("customer.in-good-standing", 9)
            ],
            input: DecisionInput.Reference("cust-42"),
            satisfied: false);

        // Act
        await sink.WriteAsync([written], CancellationToken.None);
        var read = (await sink.ReadAsync(new DecisionQuery())).ShouldHaveSingleItem();

        // Assert — the envelope
        read.Id.ShouldBe(written.Id);
        read.CorrelationId.ShouldBe(written.CorrelationId);
        read.TimestampUtc.ShouldBe(written.TimestampUtc);
        read.Caller!.ShouldBe(written.Caller!);

        // Assert — the three anchors, which are the whole point of the record
        read.RuleName.ShouldBe(written.RuleName);
        read.RuleVersion.ShouldBe(written.RuleVersion);
        read.BuildId.ShouldBe(written.BuildId);
        read.ReferencedPropositionVersions.ShouldBe(written.ReferencedPropositionVersions);

        // Assert — the outcome
        read.Outcome.Satisfied.ShouldBeFalse();
        read.Outcome.Reason.ShouldBe(written.Outcome.Reason);
        read.Outcome.Assertions.ShouldBe(written.Outcome.Assertions);
        read.Outcome.Justification.ShouldBe(written.Outcome.Justification);
        read.Outcome.Explanation.Underlying.ShouldHaveSingleItem()
            .Assertions.ShouldBe(written.Outcome.Explanation.Underlying[0].Assertions);
    }

    [Fact]
    public async Task Should_round_trip_a_reference_capture_as_the_key_it_stored()
    {
        // Arrange — the GDPR-clean posture, and the one whose value has a type worth preserving
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();

        // Act
        await sink.WriteAsync([Decisions.Record(input: DecisionInput.Reference("cust-42"))], CancellationToken.None);
        var read = (await sink.ReadAsync(new DecisionQuery())).ShouldHaveSingleItem();

        // Assert
        read.Input.ShouldNotBeNull();
        read.Input.Kind.ShouldBe(DecisionInputKind.Reference);
        read.Input.Value.ShouldBe("cust-42");
    }

    [Theory]
    [InlineData(DecisionInputKind.Whole)]
    [InlineData(DecisionInputKind.Redacted)]
    public async Task Should_round_trip_a_captured_model_as_json(DecisionInputKind kind)
    {
        // Arrange — Whole and Redacted both carry an arbitrary object, so what comes back is a
        // JsonElement rather than the adopter's type. Documented rather than fixed: the alternative
        // is pinning the adopter's assembly identity into their compliance record.
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();
        var model = new { CustomerId = "cust-42", Tier = "gold" };
        var input = kind is DecisionInputKind.Whole
            ? DecisionInput.Whole(model)
            : DecisionInput.Redacted(model);

        // Act
        await sink.WriteAsync([Decisions.Record(input: input)], CancellationToken.None);
        var read = (await sink.ReadAsync(new DecisionQuery())).ShouldHaveSingleItem();

        // Assert
        read.Input.ShouldNotBeNull();
        read.Input.Kind.ShouldBe(kind);
        var element = read.Input.Value.ShouldBeOfType<JsonElement>();
        element.GetProperty("CustomerId").GetString()!.ShouldBe("cust-42");
    }

    [Fact]
    public async Task Should_round_trip_a_record_that_captured_nothing()
    {
        // Arrange — null Input is "no capture posture applied", distinct from a posture that
        // captured null, and the column has to keep the two apart
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();

        // Act
        await sink.WriteAsync([Decisions.Record(input: null)], CancellationToken.None);
        var read = (await sink.ReadAsync(new DecisionQuery())).ShouldHaveSingleItem();

        // Assert
        read.Input.ShouldBeNull();
    }

    [Fact]
    public async Task Should_round_trip_a_record_with_no_caller()
    {
        // Arrange — nothing named the subject, which is an ordinary case rather than an error
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();

        // Act
        await sink.WriteAsync([Decisions.Record(caller: null)], CancellationToken.None);

        // Assert
        (await sink.ReadAsync(new DecisionQuery())).ShouldHaveSingleItem().Caller.ShouldBeNull();
    }

    [Fact]
    public async Task Should_write_a_whole_batch()
    {
        // Arrange
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();
        var batch = Enumerable.Range(0, 64)
            .Select(index => Decisions.Record(correlationId: $"corr-{index}"))
            .ToList();

        // Act
        await sink.WriteAsync(batch, CancellationToken.None);

        // Assert
        (await sink.ReadAsync(new DecisionQuery { Limit = 100 })).Count.ShouldBe(64);
    }

    [Fact]
    public async Task Should_keep_gaps_out_of_the_records()
    {
        // Arrange — a gap is evidence about the log, not a decision. Counting it among decisions
        // would corrupt every query the log exists to answer.
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();
        var gap = new DecisionGap(
            new DateTimeOffset(2026, 8, 25, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 25, 14, 0, 5, TimeSpan.Zero),
            17);

        // Act
        await sink.WriteAsync([Decisions.Record()], CancellationToken.None);
        await sink.WriteGapAsync(gap, CancellationToken.None);

        // Assert
        (await sink.ReadAsync(new DecisionQuery())).ShouldHaveSingleItem();
        var read = (await sink.ReadGapsAsync()).ShouldHaveSingleItem();
        read.FirstDroppedUtc.ShouldBe(gap.FirstDroppedUtc);
        read.LastDroppedUtc.ShouldBe(gap.LastDroppedUtc);
        read.DroppedCount.ShouldBe(17);
    }

    [Fact]
    public async Task Should_filter_by_correlation_id()
    {
        // Arrange — the pivot from one decision to every rule that took part in it
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();
        await sink.WriteAsync(
        [
            Decisions.Record(correlationId: "trace-1", ruleName: "checkout.can-checkout"),
            Decisions.Record(correlationId: "trace-1", ruleName: "fraud.screening"),
            Decisions.Record(correlationId: "trace-2", ruleName: "checkout.can-checkout")
        ], CancellationToken.None);

        // Act
        var read = await sink.ReadAsync(new DecisionQuery { CorrelationId = "trace-1" });

        // Assert
        read.Select(record => record.RuleName)
            .ShouldBe(["checkout.can-checkout", "fraud.screening"], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_filter_by_rule_name()
    {
        // Arrange
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();
        await sink.WriteAsync(
        [
            Decisions.Record(ruleName: "checkout.can-checkout"),
            Decisions.Record(ruleName: "fraud.screening")
        ], CancellationToken.None);

        // Act
        var read = await sink.ReadAsync(new DecisionQuery { RuleName = "fraud.screening" });

        // Assert
        read.ShouldHaveSingleItem().RuleName.ShouldBe("fraud.screening");
    }

    [Fact]
    public async Task Should_filter_by_a_time_range()
    {
        // Arrange — "why was this customer declined, on the 3rd, at 14:07?"
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();
        var noon = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        await sink.WriteAsync(
        [
            Decisions.Record(correlationId: "before", timestampUtc: noon.AddHours(-1)),
            Decisions.Record(correlationId: "inside", timestampUtc: noon),
            Decisions.Record(correlationId: "after", timestampUtc: noon.AddHours(1))
        ], CancellationToken.None);

        // Act
        var read = await sink.ReadAsync(new DecisionQuery
        {
            FromUtc = noon.AddMinutes(-1),
            ToUtc = noon.AddMinutes(1)
        });

        // Assert
        read.ShouldHaveSingleItem().CorrelationId.ShouldBe("inside");
    }

    [Fact]
    public async Task Should_return_the_newest_first_and_respect_the_limit()
    {
        // Arrange — a bounded page, newest first, because the question is always about a recent
        // decision and the table is machine-rate
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();
        var noon = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        await sink.WriteAsync(
        [
            Decisions.Record(correlationId: "oldest", timestampUtc: noon),
            Decisions.Record(correlationId: "middle", timestampUtc: noon.AddMinutes(1)),
            Decisions.Record(correlationId: "newest", timestampUtc: noon.AddMinutes(2))
        ], CancellationToken.None);

        // Act
        var read = await sink.ReadAsync(new DecisionQuery { Limit = 2 });

        // Assert
        read.Select(record => record.CorrelationId).ShouldBe(["newest", "middle"]);
    }

    [Fact]
    public async Task Should_filter_by_the_verdict()
    {
        // Arrange — "show me the declines", which is why Satisfied has a column of its own rather
        // than living only inside the outcome JSON
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();
        await sink.WriteAsync(
        [
            Decisions.Record(correlationId: "allowed", satisfied: true),
            Decisions.Record(correlationId: "declined", satisfied: false)
        ], CancellationToken.None);

        // Act
        var read = await sink.ReadAsync(new DecisionQuery { Satisfied = false });

        // Assert
        read.ShouldHaveSingleItem().CorrelationId.ShouldBe("declined");
    }

    [Fact]
    public void Should_refuse_a_query_limit_below_one()
    {
        // Act — a page of nothing is a caller mistake, not an empty result
        var act = () => new DecisionQuery { Limit = 0 };

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public async Task Should_ignore_an_empty_batch()
    {
        // Arrange — DecisionLog never sends one, but a hand-rolled caller might, and opening a
        // transaction to write nothing is worse than checking
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = database.Sink();

        // Act
        await sink.WriteAsync([], CancellationToken.None);

        // Assert
        (await sink.ReadAsync(new DecisionQuery())).ShouldBeEmpty();
    }

}
