using System.Threading;
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
/// <typeparam name="TMetadata">The type of the metadata, which is also the type of the underlying metadata
/// associated with the boolean result.</typeparam>
internal sealed class MinimalPolicyResultPredicateProposition<TModel, TMetadata>(
    Func<TModel, PolicyResultBase<TMetadata>> underlyingPolicyResultPredicate,
    Func<TModel, PolicyResultBase<TMetadata>, TMetadata> whenTrue,
    Func<TModel, PolicyResultBase<TMetadata>, TMetadata> whenFalse,
    ISpecDescription specDescription)
    : PolicyBase<TModel, TMetadata>
{
    /// <summary>
    /// Gets an empty collection of underlying propositions, since there are no underlying specifications.
    /// </summary>
    public override IEnumerable<SpecBase> Underlying => [];

    /// <summary>Gets the name of the proposition.</summary>
    public override ISpecDescription Description => specDescription;

    public override bool Matches(TModel model) => underlyingPolicyResultPredicate(model).Satisfied;

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model)
    {
        var booleanResult = underlyingPolicyResultPredicate(model);
        PolicyResultBase<TMetadata>[] booleanResults = [booleanResult];

        var metadataResolver =
            booleanResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        var metadata = new Lazy<TMetadata>(() => metadataResolver(model, booleanResult), LazyThreadSafetyMode.None);

        var assertions = new Lazy<string>(() =>
            metadata.Value switch
            {
                string assertion => assertion,
                _ => Description.ToReason(booleanResult.Satisfied)
            }, LazyThreadSafetyMode.None);

        return new PolicyResultWithUnderlying<TMetadata,TMetadata>(
            booleanResult,
            () => metadata.Value,
            () => new MetadataNode<TMetadata>(metadata.Value,
                booleanResults as IEnumerable<PolicyResultBase<TMetadata>> ?? []),
            () => new Explanation(
                assertions.Value,
                booleanResults,
                booleanResults),
            () => new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                Description.ToReason(booleanResult.Satisfied),
                Description.Statement));
    }
}
