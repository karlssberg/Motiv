using System.Threading;
using Motiv.Shared;

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
internal sealed class BooleanResultPredicateProposition<TModel, TMetadata, TUnderlyingMetadata>(
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

    public override bool Matches(TModel model) => underlyingBooleanResultPredicate(model).Satisfied;

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model)
    {
        var booleanResult = underlyingBooleanResultPredicate(model);
        BooleanResultBase<TUnderlyingMetadata>[] booleanResults = [booleanResult];

        var metadataResolver =
            booleanResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        var metadata = new Lazy<TMetadata>(() => metadataResolver(model, booleanResult), LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            Description.ToReason(booleanResult.Satisfied), LazyThreadSafetyMode.None);

        return new PolicyResultWithUnderlying<TMetadata,TUnderlyingMetadata>(
            booleanResult,
            () => metadata.Value,
            () => new MetadataNode<TMetadata>(metadata.Value,
                booleanResults as IEnumerable<BooleanResultBase<TMetadata>> ?? []),
            () => new Explanation(
                assertion.Value,
                booleanResults,
                booleanResults),
            () => new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                assertion.Value,
                Description.Statement));
    }
}
