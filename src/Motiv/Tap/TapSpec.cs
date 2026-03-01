namespace Motiv.Tap;

internal sealed class TapSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> operand,
    Action<TModel, BooleanResultBase<TMetadata>> callback)
    : SpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => operand.ToEnumerable();

    public override ISpecDescription Description => operand.Description;

    public override bool Matches(TModel model) => operand.Matches(model);

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model)
    {
        var result = operand.IsSatisfiedBy(model);
        callback(model, result);
        return result;
    }
}
