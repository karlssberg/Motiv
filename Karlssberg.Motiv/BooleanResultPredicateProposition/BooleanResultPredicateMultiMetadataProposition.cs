namespace Karlssberg.Motiv.BooleanResultPredicateProposition;

public sealed class BooleanResultPredicateMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> underlyingBooleanResultPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription)
    : SpecBase<TModel, TMetadata>
{
    /// <summary>Gets the name of the proposition.</summary>
    public override ISpecDescription Description => specDescription;

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

        var assertions = metadata switch
        {
            IEnumerable<string> assertion => assertion.ToArray(),
            _ => [Description.ToReason(booleanResult.Satisfied)]
        };
        
        var metadataTree = new MetadataTree<TMetadata>(
            metadata,
            booleanResult.ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>());
        
        var explanation = new Explanation(assertions, assertions)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };
        
        return new BooleanResultWithUnderlying<TMetadata,TUnderlyingMetadata>(
            booleanResult,
            metadataTree,
            explanation,
            Description.ToReason(booleanResult.Satisfied));
    }
}

