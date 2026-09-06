using Motiv.Traversal;

namespace Motiv.Tap;

/// <summary>
/// A transparent wrapper that hangs a side effect off <paramref name="operand" /> without altering the
/// result it returns. The three variants differ only in when the callback fires, which is the one thing
/// <see cref="ShouldInvokeCallback" /> asks of them.
/// </summary>
/// <remarks>
/// <see cref="SpecBase{TModel,TMetadata}.Matches" /> forwards to the operand and never fires the
/// callback: the allocation-free path has no result to hand it.
/// </remarks>
internal abstract class TapSpecBase<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> operand,
    Action<TModel, BooleanResultBase<TMetadata>> callback)
    : SpecBase<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [operand];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => operand.Description;

    public override bool Matches(TModel model) => operand.Matches(model);

    /// <summary>Whether this variant's callback fires for <paramref name="result" />.</summary>
    protected abstract bool ShouldInvokeCallback(BooleanResultBase<TMetadata> result);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var result = operand.EvaluateInternal(model);

        if (ShouldInvokeCallback(result))
        {
            // A Tap is a side effect hung off a node, not part of the decision the node makes, so
            // whatever the callback evaluates is work inside that node. Charged, adding an audit hook to
            // a rule could make the rule itself refuse — the failure attributed to the decision rather
            // than to the observability that caused it.
            using var exclusion = EvaluationBudget.Exclude();
            callback(model, result);
        }

        return result;
    }
}
