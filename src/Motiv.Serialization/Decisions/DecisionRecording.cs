using Motiv.Shared;

namespace Motiv.Serialization;

/// <summary>
/// Assembles and enqueues the record one evaluation leaves behind. Shared by the synchronous and
/// asynchronous rule flavours, which reach it from four entry points between them.
/// </summary>
/// <remarks>
/// <para>
/// Shared rather than duplicated because it is genuinely one behaviour: unlike the builder paths, whose
/// duplication is deliberate and whose bodies really do differ, these two were identical to the
/// character. What each flavour keeps for itself is how it gets hold of the state and the result —
/// which is exactly where they differ.
/// </para>
/// <para>
/// The outcome is projected here, on whichever thread evaluated, rather than on the background writer.
/// Deferring it would move real cost off the request path, but the result tree memoises as it is read
/// and none of that memoisation is documented thread-safe: handing a half-read result to a writer
/// thread races the caller still reading it, in the one subsystem whose output is the product. What
/// crosses the queue is immutable.
/// </para>
/// </remarks>
internal static class DecisionRecording
{
    /// <summary>
    /// Every authored proposition a rule resolves through, transitively, at the version it has in
    /// <paramref name="generation"/> — a record's third anchor.
    /// </summary>
    /// <remarks>
    /// Callers cache this per bound state rather than calling it per evaluation, and that is sound
    /// rather than a shortcut: republishing anything in the closure rebinds every referrer and
    /// produces a new state, so the answer cannot go stale while the state it belongs to is live.
    /// </remarks>
    public static IReadOnlyList<PropositionVersion> ResolvePropositionPin(
        ScopeGeneration generation, string ruleName)
    {
        var references = generation.Graph.ReferenceClosure(NodeId.Rule(ruleName));
        if (references.Count == 0)
            return [];

        var pinned = new List<PropositionVersion>(references.Count);
        foreach (var reference in references)
        {
            // A name resolving to a compiled spec rather than an authored proposition has no version
            // of its own; BuildId is what pins those, which is why it is a separate anchor.
            if (generation.Authored.TryGetValue(reference, out var authored))
                pinned.Add(new PropositionVersion(authored.Name, authored.Version));
        }

        return pinned;
    }

    public static void Write<TModel, TMetadata>(
        DecisionLog log,
        string ruleName,
        int ruleVersion,
        IReadOnlyList<PropositionVersion> propositionPin,
        TModel model,
        BooleanResultBase<TMetadata> result)
    {
        var decision = DecisionSnapshot.Current;

        log.Enqueue(new DecisionRecord(
            Id: Guid.NewGuid(),
            // A decision always has an identity, pinned or not: a record from a single-rule evaluation
            // still has to be findable.
            CorrelationId: decision?.CorrelationId ?? Guid.NewGuid().ToString("N"),
            TimestampUtc: DateTimeOffset.UtcNow,
            Caller: decision?.Caller,
            RuleName: ruleName,
            RuleVersion: ruleVersion,
            BuildId: BuildIdentity.Current,
            ReferencedPropositionVersions: propositionPin,
            Input: log.Capture.Capture(model),
            Outcome: ResultProjection.ProjectUntyped(result)));
    }
}
