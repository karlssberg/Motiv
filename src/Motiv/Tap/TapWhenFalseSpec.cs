namespace Motiv.Tap;

internal sealed class TapWhenFalseSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> operand,
    Action<TModel, BooleanResultBase<TMetadata>> callback)
    : SpecBase<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [operand];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => operand.Description;

    public override bool Matches(TModel model) => operand.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var result = operand.Evaluate(model);
        if (!result.Satisfied)
            callback(model, result);
        return result;
    }
}
