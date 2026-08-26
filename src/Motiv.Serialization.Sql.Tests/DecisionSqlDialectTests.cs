using Shouldly;
using Xunit;

namespace Motiv.Serialization.Sql.Tests;

/// <summary>
/// The three dialects, proved structurally. Behavioural conformance runs on SQLite alone — the same
/// split the authoring store's <c>ProviderSchemaTests</c> uses — so what is left to prove is the SQL
/// text itself, on the three engines ticket 16 named.
/// </summary>
public class DecisionSqlDialectTests
{
    public static TheoryData<string, DecisionSqlDialect> Dialects => new()
    {
        { "SQLite", DecisionSqlDialect.Sqlite },
        { "PostgreSQL", DecisionSqlDialect.PostgreSql },
        { "SQL Server", DecisionSqlDialect.SqlServer }
    };

    [Theory]
    [MemberData(nameof(Dialects))]
    public void Should_create_both_tables_and_both_indexes(string name, DecisionSqlDialect dialect)
    {
        // Act
        var schema = string.Join("\n", new DecisionStatements(dialect).Schema);

        // Assert — two tables, because a gap is evidence about the log rather than a decision
        schema.ShouldContain(DecisionSchema.DecisionTable, customMessage: name);
        schema.ShouldContain(DecisionSchema.GapTable, customMessage: name);

        // Assert — and only the two indexes an append-heavy table can afford: the purge's own
        // predicate, and the pivot from one decision to every rule that took part in it
        schema.ShouldContain(DecisionSchema.TimestampUtc, customMessage: name);
        schema.ShouldContain(DecisionSchema.CorrelationId, customMessage: name);
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void Should_create_its_schema_only_when_absent(string name, DecisionSqlDialect dialect)
    {
        // Act — a replica restarting against a live table must not fail here, and there is no
        // migration engine behind this to make it not matter
        var schema = string.Join("\n", new DecisionStatements(dialect).Schema).ToUpperInvariant();

        // Assert — SQL Server has no IF NOT EXISTS on CREATE TABLE, so it guards instead
        var guarded = schema.Contains("IF NOT EXISTS") || schema.Contains("OBJECT_ID");
        guarded.ShouldBeTrue($"{name} creates its schema unconditionally");
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void Should_bound_the_purge_to_a_batch(string name, DecisionSqlDialect dialect)
    {
        // Act
        var purge = new DecisionStatements(dialect).PurgeDecisions.ToUpperInvariant();

        // Assert — an unbounded DELETE after a long outage holds one lock for minutes
        purge.ShouldContain("DELETE", customMessage: name);
        purge.ShouldContain("@BATCH", customMessage: name);
        purge.ShouldContain("@CUTOFF", customMessage: name);
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void Should_purge_gaps_by_their_last_drop(string name, DecisionSqlDialect dialect)
    {
        // Act
        var purge = new DecisionStatements(dialect).PurgeGaps;

        // Assert — keyed on the last drop, so a run straddling the cutoff survives until all of it
        // is past the window
        purge.ShouldContain(DecisionSchema.LastDroppedUtc, customMessage: name);
        purge.ShouldContain(DecisionSchema.GapTable, customMessage: name);
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void Should_bound_a_read_to_one_page(string name, DecisionSqlDialect dialect)
    {
        // Act
        var select = new DecisionStatements(dialect).SelectDecisions(new DecisionQuery());

        // Assert — newest first and capped; the table is machine-rate and the question is always
        // about a recent decision
        select.ToUpperInvariant().ShouldContain("ORDER BY", customMessage: name);
        select.ShouldContain("@limit", customMessage: name);
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void Should_add_a_predicate_per_filter(string name, DecisionSqlDialect dialect)
    {
        // Arrange
        var statements = new DecisionStatements(dialect);

        // Act — an absent filter must not become a parameter the reader never binds
        var unfiltered = statements.SelectDecisions(new DecisionQuery());
        var filtered = statements.SelectDecisions(new DecisionQuery
        {
            CorrelationId = "trace-1",
            RuleName = "checkout.can-checkout",
            FromUtc = DateTimeOffset.UnixEpoch,
            ToUtc = DateTimeOffset.UnixEpoch
        });

        // Assert
        unfiltered.ShouldNotContain("@correlationId", customMessage: name);
        filtered.ShouldContain("@correlationId", customMessage: name);
        filtered.ShouldContain("@ruleName", customMessage: name);
        filtered.ShouldContain("@fromUtc", customMessage: name);
        filtered.ShouldContain("@toUtc", customMessage: name);
    }

    [Fact]
    public void Should_quote_identifiers_the_way_each_engine_does()
    {
        // Assert — the one place the three genuinely disagree about syntax rather than semantics
        DecisionSqlDialect.Sqlite.Quote("MotivDecision").ShouldBe("\"MotivDecision\"");
        DecisionSqlDialect.PostgreSql.Quote("MotivDecision").ShouldBe("\"MotivDecision\"");
        DecisionSqlDialect.SqlServer.Quote("MotivDecision").ShouldBe("[MotivDecision]");
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void Should_name_every_column_the_record_carries(string name, DecisionSqlDialect dialect)
    {
        // Act
        var insert = new DecisionStatements(dialect).InsertDecision;

        // Assert — the three anchors especially: a column quietly missing here would make a record
        // unreplayable in a way nothing else in the suite would notice
        insert.ShouldContain(DecisionSchema.RuleVersion, customMessage: name);
        insert.ShouldContain(DecisionSchema.BuildId, customMessage: name);
        insert.ShouldContain(DecisionSchema.PropositionsJson, customMessage: name);
        insert.ShouldContain(DecisionSchema.Satisfied, customMessage: name);
        insert.ShouldContain(DecisionSchema.OutcomeJson, customMessage: name);
        insert.ShouldContain(DecisionSchema.InputKind, customMessage: name);
        insert.ShouldContain(DecisionSchema.InputJson, customMessage: name);
    }
}
