namespace Karlssberg.Motiv.BooleanResultPredicateProposition;

internal sealed class BooleanResultPredicateWithSingleAssertionProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate,
    string trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription specDescription)
    : SpecBase<TModel, string>
{
    public override ISpecDescription Description => specDescription;
    
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = predicate(model);
        
        var assertion = new Lazy<string>(() => booleanResult.Satisfied switch
        {
            true => trueBecause,
            false => whenFalse(model, booleanResult),
        });
        
        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            booleanResult,
            MetadataTree,
            Explanation,
            assertion.Value);

        Explanation Explanation() => new(assertion.Value)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

        MetadataTree<string> MetadataTree() =>
            new(assertion.Value.ToEnumerable(), 
                booleanResult.ResolveMetadataTrees<string, TUnderlyingMetadata>());
    }
}