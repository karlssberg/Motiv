namespace Motiv.BooleanResultPredicateProposition;

internal sealed class BooleanResultPredicateExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription specDescription)
    : SpecBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => [];
    
    public override ISpecDescription Description => specDescription;
    
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = predicate(model);
        
        var assertion = new Lazy<string>(() => 
            booleanResult.Satisfied switch
            {
                true => whenTrue(model, booleanResult),
                false => whenFalse(model, booleanResult)
            });
        
        var explanation = new Lazy<Explanation>(() => 
            new Explanation(assertion.Value, booleanResult.ToEnumerable()));
        
        var metadataTier = new Lazy<MetadataNode<string>>(() => 
            new MetadataNode<string>(
                assertion.Value.ToEnumerable(), 
                booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<string>> ?? []));

        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            booleanResult,
            MetadataTier,
            Explanation,
            Reason);

        MetadataNode<string> MetadataTier() => metadataTier.Value;
        Explanation Explanation() => explanation.Value;
        string Reason() => assertion.Value;
    }
}