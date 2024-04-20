namespace Karlssberg.Motiv.BooleanResultPredicateProposition;

public sealed class BooleanResultPredicateMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> underlyingBooleanResultPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
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
        
        var metadata = new Lazy<TMetadata>(() => booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult),
        });
        
        var assertion = new Lazy<string[]>(() => metadata.Value switch
        {
            string because => [because],
            IEnumerable<string>  because => because.ToArray(),
            _ => [Description.ToReason(booleanResult.Satisfied)]
        });
        
        var explanation = new Lazy<Explanation>(() => 
            new Explanation(assertion.Value, booleanResult.ToEnumerable()));
        
        var metadataTree = new Lazy<MetadataNode<TMetadata>>(() => 
            new MetadataNode<TMetadata>(metadata.Value.ToEnumerable(), 
                booleanResult.MetadataTiers.ToEnumerable() as IEnumerable<MetadataNode<TMetadata>> ?? []));

        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            MetadataTree,
            Explanation,
            Reason);

        MetadataNode<TMetadata> MetadataTree() => metadataTree.Value;
        Explanation Explanation() => explanation.Value;
        string Reason() => Description.ToReason(booleanResult.Satisfied);
    }
}