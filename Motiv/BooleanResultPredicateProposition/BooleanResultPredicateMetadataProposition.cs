namespace Motiv.BooleanResultPredicateProposition;

/// <summary>
/// Represents a proposition that yields a collection of metadata based on the result of a boolean predicate.
/// </summary>
/// <param name="underlyingBooleanResultPredicate">The predicate that determines the boolean result.</param>
/// <param name="whenTrue">The metadata to yield when the predicate is true.</param>
/// <param name="whenFalse">The metadata to yield when the predicate is false.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the boolean result.</typeparam>
internal sealed class BooleanResultPredicateMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> underlyingBooleanResultPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription specDescription)
    : PolicyBase<TModel, TMetadata>
{
    /// <summary>
    /// Gets an empty collection of underlying propositions, since there are no underlying specifications.
    /// </summary>
    public override IEnumerable<SpecBase> Underlying => [];

    /// <summary>Gets the name of the proposition.</summary>
    public override ISpecDescription Description => specDescription;

    public override PolicyResultBase<TMetadata> Execute(TModel model)
    {
        var booleanResult = underlyingBooleanResultPredicate(model);

        var metadata = CreateLazyMetadata(model, booleanResult);

        return CreatePolicyResult(metadata, booleanResult);
    }

    /// <summary>Determines if the proposition is satisfied by the given model.</summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>
    ///     A <see cref="BooleanResultBase{TMetadata}" /> indicating if the proposition is satisfied and the resulting
    ///     metadata.
    /// </returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) => Execute(model);

    private Lazy<TMetadata> CreateLazyMetadata(TModel model, BooleanResultBase<TUnderlyingMetadata> booleanResult)
    {
        return new Lazy<TMetadata>(() =>
            booleanResult.Satisfied switch
            {
                true => whenTrue(model, booleanResult),
                false => whenFalse(model, booleanResult)
            });
    }

    private PolicyResultBase<TMetadata> CreatePolicyResult(Lazy<TMetadata> metadata, BooleanResultBase<TUnderlyingMetadata> booleanResult)
    {
        var assertions = new Lazy<string[]>(() => metadata.Value switch
        {
            IEnumerable<string> assertion => assertion.ToArray(),
            _ => [Description.ToReason(booleanResult.Satisfied)]
        });

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertions.Value, booleanResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(metadata.Value,
                booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? []));

        return new PolicyResultWithUnderlying<TMetadata,TUnderlyingMetadata>(
            booleanResult,
            Value,
            MetadataTier,
            Explanation,
            Reason);

        TMetadata Value() => metadata.Value;
        MetadataNode<TMetadata> MetadataTier() => metadataTier.Value;
        Explanation Explanation() => explanation.Value;
        string Reason() => Description.ToReason(booleanResult.Satisfied);
    }
}
