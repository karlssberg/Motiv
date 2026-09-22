namespace Motiv.Serialization.Sql.Tests;

/// <summary>Builds decision records for the tests, so a suite asserts on one field at a time.</summary>
internal static class Decisions
{
    /// <summary>
    /// A record with every field populated, overridable one at a time. <c>input</c> is the exception:
    /// it defaults to null, which is "no capture posture applied" — the state a test asserting on
    /// capture has to be able to reach.
    /// </summary>
    public static DecisionRecord Record(
        Guid? id = null,
        string correlationId = "corr-1",
        DateTimeOffset? timestampUtc = null,
        string? caller = "alice",
        string ruleName = "checkout.can-checkout",
        int ruleVersion = 7,
        string buildId = "build-42",
        IReadOnlyList<PropositionVersion>? referenced = null,
        DecisionInput? input = null,
        bool satisfied = true) =>
        new(
            id ?? Guid.NewGuid(),
            correlationId,
            timestampUtc ?? DateTimeOffset.UtcNow,
            caller,
            ruleName,
            ruleVersion,
            buildId,
            referenced ?? [new PropositionVersion("customer.is-active", 3)],
            input,
            Outcome(satisfied));

    /// <summary>An outcome payload with a two-level explanation tree, so the JSON is not a leaf.</summary>
    public static RuleEvaluationResult<object?> Outcome(bool satisfied = true) =>
        new(
            satisfied,
            satisfied ? "(is active) & (in good standing)" : "!(is active)",
            satisfied ? ["is active", "in good standing"] : ["is not active"],
            satisfied ? ["active"] : ["inactive"],
            satisfied ? "AND\n    is active\n    in good standing" : "is not active",
            new ExplanationNode(
                satisfied ? ["is active", "in good standing"] : ["is not active"],
                [new ExplanationNode(satisfied ? ["is active"] : ["is not active"], [])]));
}
