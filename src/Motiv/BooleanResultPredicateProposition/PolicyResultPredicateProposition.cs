using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition;

/// <summary>
/// Represents a proposition that yields a collection of metadata based on the result of a boolean predicate.
/// </summary>
/// <param name="underlyingPolicyResultPredicate">The predicate that determines the boolean result.</param>
/// <param name="whenTrue">The metadata to yield when the predicate is true.</param>
/// <param name="whenFalse">The metadata to yield when the predicate is false.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the boolean result.</typeparam>
internal sealed class PolicyResultPredicateProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> underlyingPolicyResultPredicate,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription specDescription)
    : PolicyBase<TModel, TMetadata>
{
    /// <summary>
    /// Gets an empty collection of underlying propositions, since there are no underlying specifications.
    /// </summary>
    public override IEnumerable<SpecBase> Underlying => [];

    /// <summary>Gets the name of the proposition.</summary>
    public override ISpecDescription Description => specDescription;

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var booleanResult = underlyingPolicyResultPredicate(model);
        var metadataResolver =
            booleanResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        var metadata = new Lazy<TMetadata>(() => metadataResolver(model, booleanResult));

        var assertions = new Lazy<string>(() =>
            metadata.Value switch
            {
                string assertion => assertion,
                _ => Description.ToReason(booleanResult.Satisfied)
            });

        var reason = new Lazy<string>(() =>
            metadata.Value switch
            {
                string reason when !Description.HasExplicitStatement => reason,
                _ => Description.ToReason(booleanResult.Satisfied)
            });

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(
                assertions.Value,
                booleanResult.ToEnumerable(),
                booleanResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(metadata.Value,
                booleanResult.ToEnumerable() as IEnumerable<PolicyResultBase<TMetadata>> ?? []));

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                reason.Value,
                Description.Statement));

        return new PolicyResultWithUnderlying<TMetadata,TUnderlyingMetadata>(
            booleanResult,
            () => metadata.Value,
            () => metadataTier.Value,
            () => explanation.Value,
            () => resultDescription.Value);
    }
}
