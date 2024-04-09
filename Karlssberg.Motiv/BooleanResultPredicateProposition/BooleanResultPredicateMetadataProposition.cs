namespace Karlssberg.Motiv.BooleanResultPredicateProposition;

public sealed class BooleanResultPredicateMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> underlyingBooleanResultPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    string propositionalAssertion)
    : SpecBase<TModel, TMetadata>
{
    /// <summary>Gets the name of the proposition.</summary>
    public override ISpecDescription Description => new SpecDescription(propositionalAssertion);

    /// <summary>Determines if the proposition is satisfied by the given model.</summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>
    ///     A <see cref="BooleanResultBase{TMetadata}" /> indicating if the proposition is satisfied and the resulting
    ///     metadata.
    /// </returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingBooleanResultPredicate(model);
        
        var metadata = booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult),
        };
        
        var assertion = metadata switch {
            string because => because,
            _ => Description.ToReason(booleanResult.Satisfied)
        };
        
        var metadataTree = new MetadataTree<TMetadata>(
            metadata,
            booleanResult.ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>());

        var explanation = new Explanation(assertion)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };
        
        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            metadataTree,
            explanation,
            Description.ToReason(booleanResult.Satisfied));
    }
}