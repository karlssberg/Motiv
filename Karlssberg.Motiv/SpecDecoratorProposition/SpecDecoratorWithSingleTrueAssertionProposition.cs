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
        
        var assertion = new Lazy<string>(() => 
            booleanResult.Satisfied switch
            {
                true => trueBecause,
                false => whenFalse(model, booleanResult)
            });
        
        var reason = propositionalStatement is not null
            ? propositionalStatement.ToReason(booleanResult.Satisfied)
            : assertion.Value;
        
        var explanation = new Lazy<Explanation>(() => 
            new Explanation(assertion.Value, booleanResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() => 
            new MetadataNode<string>(assertion.Value, 
                booleanResult.MetadataTier.ToEnumerable() as IEnumerable<MetadataNode<string>> ?? []));

        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            booleanResult,
            MetadataTier,
            Explanation,
            Reason);

        MetadataNode<string> MetadataTier() => metadataTier.Value;
        Explanation Explanation() => explanation.Value;
        string Reason() => reason;
    }
}