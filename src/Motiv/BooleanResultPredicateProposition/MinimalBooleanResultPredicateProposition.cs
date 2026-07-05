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
/// <typeparam name="TMetadata">The type of the metadata, which is also the type of the underlying metadata
/// associated with the boolean result.</typeparam>
internal sealed class MinimalBooleanResultPredicateProposition<TModel, TMetadata>(
    Func<TModel, BooleanResultBase<TMetadata>> underlyingBooleanResultPredicate,
    Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription)
    : SpecBase<TModel, TMetadata>
{
    /// <summary>
    /// Gets an empty collection of underlying propositions, since there are no underlying specifications.
    /// </summary>
    public override IEnumerable<SpecBase> Underlying => [];

    /// <summary>Gets the name of the proposition.</summary>
    public override ISpecDescription Description => specDescription;

    public override bool Matches(TModel model) => underlyingBooleanResultPredicate(model).Satisfied;

    /// <summary>Determines if the proposition is satisfied by the given model.</summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>
    ///     A <see cref="BooleanResultBase{TMetadata}" /> indicating if the proposition is satisfied and the resulting
    ///     metadata.
    /// </returns>
    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var booleanResult = underlyingBooleanResultPredicate(model);
        BooleanResultBase<TMetadata>[] booleanResults = [booleanResult];

        var metadataResolver =
            booleanResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        var metadata = new Lazy<TMetadata[]>(() => metadataResolver(model, booleanResult).ToArray(), LazyThreadSafetyMode.None);

        var assertions = new Lazy<string[]>(() =>
            metadata.Value switch
            {
                IEnumerable<string> assertion => assertion.ToArray(),
                _ => [Description.ToReason(booleanResult.Satisfied)]
            }, LazyThreadSafetyMode.None);

        return new BooleanResultWithUnderlying<TMetadata,TMetadata>(
            booleanResult,
            () => new MetadataNode<TMetadata>(metadata.Value,
                booleanResults as IEnumerable<BooleanResultBase<TMetadata>> ?? []),
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
