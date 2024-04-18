namespace Karlssberg.Motiv.BooleanResultPredicateProposition;

internal sealed class BooleanResultPredicateExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription specDescription)
    : SpecBase<TModel, string>
{
    public override ISpecDescription Description => specDescription;
    
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = predicate(model);
        
        var assertion = new Lazy<string>(() => 
            booleanResult.Satisfied switch
            {
                true => whenTrue(model, booleanResult),
                false => whenFalse(model, booleanResult),
            });
        
        var explanation = new Lazy<Explanation>(() => 
            new Explanation(assertion.Value)
            {
                Underlying = booleanResult.FindPropositionalExplanations()
            });
        
        var metadataTree = new Lazy<MetadataTree<string>>(() => 
            new MetadataTree<string>(
                assertion.Value.ToEnumerable(), 
                booleanResult.ResolveMetadataTrees<string, TUnderlyingMetadata>()));

        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            booleanResult,
            MetadataTree,
            Explanation,
            Reason);

        MetadataTree<string> MetadataTree() => metadataTree.Value;
        Explanation Explanation() => explanation.Value;
        string Reason() => specDescription.ToReason(booleanResult.Satisfied);
    }
}