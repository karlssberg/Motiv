namespace Karlssberg.Motiv.ElseIf;

internal class ElseIfSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> antecedent,
    SpecBase<TModel, TMetadata> consequent)
    : SpecBase<TModel, TMetadata>
{
    public override string Description => $"({antecedent}) => ({consequent})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var antecedentResult = antecedent.IsSatisfiedByOrWrapException(model);
        return antecedentResult.Value switch
        {
            true => antecedentResult,
            false => consequent.IsSatisfiedByOrWrapException(model)
        };
    }
}