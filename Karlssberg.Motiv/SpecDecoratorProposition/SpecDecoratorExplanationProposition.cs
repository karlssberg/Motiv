namespace Karlssberg.Motiv.SpecDecoratorProposition;

internal sealed class SpecDecoratorExplanationProposition<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause,
    ISpecDescription description)
    : SpecBase<TModel, string>
{
    public override ISpecDescription Description => description;

    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; } = underlyingSpec;

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = UnderlyingSpec.IsSatisfiedBy(model);

        var assertion = new Lazy<string>(() =>
            booleanResult.Satisfied switch
            {
                true => trueBecause(model, booleanResult),
                false => falseBecause(model, booleanResult)
            });

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertion.Value, booleanResult.ToEnumerable()));
        
        var metadataTree = new Lazy<MetadataNode<string>>(() => 
            new MetadataNode<string>(assertion.Value.ToEnumerable(), 
                booleanResult.MetadataTier.ToEnumerable() as IEnumerable<MetadataNode<string>> ?? []));

        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            booleanResult, 
            MetadataTree,
            Explanation,
            Reason);
        
        MetadataNode<string> MetadataTree() => metadataTree.Value;
        Explanation Explanation() => explanation.Value;
        string Reason() => Description.ToReason(booleanResult.Satisfied);
    }
}