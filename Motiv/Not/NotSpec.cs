namespace Motiv.Not;

internal sealed class NotSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> operand)
    : SpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => operand.ToEnumerable();

    public override ISpecDescription Description =>
        new NotSpecDescription<TModel, TMetadata>(operand);

    internal override BooleanResultBase<TMetadata> IsSatisfiedByInternal(TModel model) =>
        operand.IsSatisfiedBy(model).Not();
}
