namespace Motiv.Not;

internal sealed class NotSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> operand)
    : SpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => operand.ToEnumerable();
    
    public override ISpecDescription Description => 
        new NotSpecDescription<TModel, TMetadata>(operand);

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) => 
        operand.IsSatisfiedBy(model).Not();
}