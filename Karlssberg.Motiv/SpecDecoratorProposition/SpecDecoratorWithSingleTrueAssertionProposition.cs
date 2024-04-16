namespace Karlssberg.Motiv.SpecDecoratorProposition;

internal sealed class SpecDecoratorWithSingleTrueAssertionProposition<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    string trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    string? propositionalStatement = null)
    : SpecBase<TModel, string>
{
    public override ISpecDescription Description => new SpecDescription(
        propositionalStatement ?? trueBecause,
        underlyingSpec.Description.Detailed);

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);
        
        var assertion = new Lazy<string>(() => booleanResult.Satisfied switch
        {
            true => trueBecause,
            false => whenFalse(model, booleanResult)
        });

        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            booleanResult,
            MetadataTree,
            Explanation,
            propositionalStatement is null
                ? trueBecause
                : propositionalStatement.ToReason(booleanResult.Satisfied));

        Explanation Explanation() => new(assertion.Value)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

        MetadataTree<string> MetadataTree() => 
            new(assertion.Value, 
                booleanResult.ResolveMetadataTrees<string, TUnderlyingMetadata>());
    }
}