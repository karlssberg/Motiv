using System.Diagnostics;
using Motiv.Diagnostics;

namespace Motiv.Serialization;

/// <summary>
/// Walks an audited rule's result tree, emitting one span per causal node under the rule's own span.
/// </summary>
/// <remarks>
/// <para>
/// <strong>These spans carry structure, not timing.</strong> Motiv evaluates a composition in one
/// pass and never times a sub-proposition, so a node span's duration is the walk that emitted it and
/// nothing else. What is real is the shape: which sub-propositions were causal, how they nest, and
/// which way each one went. Read the waterfall for the tree, not for where the time went —
/// <c>motiv.evaluate</c>'s own duration is the only honest number in it.
/// </para>
/// <para>
/// The walk is iterative over an explicit stack rather than recursive. A result tree has no small
/// upper bound, and a recursive walk over a deep one is precisely the uncatchable crash Spec 3A
/// removed from the result-tree properties; reintroducing one inside instrumentation — where it would
/// only ever fire in production, under a listener — would be the worst possible place for it.
/// </para>
/// </remarks>
internal static class NodeSpanWriter
{
    /// <summary>Emits the tree under <paramref name="evaluation"/>, bounded by <paramref name="budget"/>.</summary>
    /// <param name="evaluation">The rule's own span, which every node hangs beneath.</param>
    /// <param name="result">The result whose causal tree is walked.</param>
    /// <param name="budget">The most node spans this evaluation may emit.</param>
    public static void Write(Activity evaluation, BooleanResultBase result, int budget)
    {
        var detail = MotivTelemetry.ExplanationDetail;
        var emitted = 0;

        var pending = new Stack<(BooleanResultBase Node, ActivityContext Parent)>();
        PushCauses(pending, result, evaluation.Context);

        while (pending.Count > 0)
        {
            if (emitted == budget)
            {
                // Said out loud rather than simply stopping. A waterfall that quietly stops short
                // reads as a complete picture of a smaller tree, which is worse than no picture.
                evaluation.SetTag("motiv.rules.nodes.truncated", true);
                return;
            }

            var (node, parent) = pending.Pop();

            using var span = MotivRulesTelemetry.ActivitySource.StartActivity(
                MotivRulesTelemetry.NodeActivityName, ActivityKind.Internal, parent);

            // A sampler that declined this child would decline its siblings too, and half a tree is
            // not a smaller tree — it is a misleading one.
            if (span is null)
                return;

            span.SetTag("motiv.satisfied", node.Satisfied);
            TrySetReason(span, node, detail);
            emitted++;

            PushCauses(pending, node, span.Context);
        }
    }

    /// <summary>
    /// Queues a node's causal children. <c>Causes</c>, not <c>Underlying</c>: the de-noised set is
    /// what actually carried the outcome, and it is the same set every other explanation surface
    /// reports — a trace that disagreed with the <c>Justification</c> beside it would be worse than
    /// no trace.
    /// </summary>
    private static void PushCauses(
        Stack<(BooleanResultBase Node, ActivityContext Parent)> pending,
        BooleanResultBase node,
        ActivityContext parent)
    {
        foreach (var cause in node.Causes)
            pending.Push((cause, parent));
    }

    /// <summary>
    /// Tags a node with its own reason, to the extent the explanation-tag mode allows — and never
    /// under <see cref="ExplanationDetail.None"/>.
    /// </summary>
    /// <remarks>
    /// Node spans are explanation text by another name, so they are governed by the same control and
    /// therefore by the same capture posture. A node tree that leaked assertion text a decision
    /// record was forbidden to store would defeat the coupling exactly where it matters most: there
    /// is one span per node here, so this is the widest exposure of that text anywhere. Resolution
    /// runs a user's WhenTrue/WhenFalse delegate and can throw; as in core, that must never turn an
    /// evaluation that already succeeded into a failing one.
    /// </remarks>
    private static void TrySetReason(Activity span, BooleanResultBase node, ExplanationDetail detail)
    {
        if (detail == ExplanationDetail.None)
            return;

        try
        {
            span.SetTag("motiv.reason", node.Reason);
        }
        catch
        {
            // See the remarks: the span keeps the outcome, and loses only the text.
        }
    }
}
