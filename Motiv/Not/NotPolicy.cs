namespace Motiv.Not;

internal sealed class NotPolicy<TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> operand)
    : PolicyBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => operand.ToEnumerable();

    public override ISpecDescription Description =>
        new NotSpecDescription<TModel, TMetadata>(operand);

    public override PolicyResultBase<TMetadata> IsSatisfiedBy(TModel model) =>
        operand.IsSatisfiedBy(model).Not();
}
