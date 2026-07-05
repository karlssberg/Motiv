using System.Threading;
using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition;

/// <summary>
/// Represents a proposition that yields a collection of assertions based on the result of a boolean predicate. The
/// because-strings double as the assertions; degenerate (null/empty/whitespace) strings fall back to the
/// statement-derived reason.
/// </summary>
/// <param name="underlyingBooleanResultPredicate">The predicate that determines the boolean result.</param>
/// <param name="whenTrue">The assertions to yield when the predicate is true.</param>
/// <param name="whenFalse">The assertions to yield when the predicate is false.</param>
/// <param name="specDescription">The description of the proposition.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the boolean result.</typeparam>
internal sealed class BooleanResultPredicateMultiAssertionExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> underlyingBooleanResultPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> whenFalse,
    ISpecDescription specDescription)
    : SpecBase<TModel, string>
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
    protected override BooleanResultBase<string> EvaluateSpec(TModel model)
    {
        var booleanResult = underlyingBooleanResultPredicate(model);
        BooleanResultBase<TUnderlyingMetadata>[] booleanResults = [booleanResult];

        var metadataResolver =
            booleanResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        var metadata = new Lazy<string[]>(() => metadataResolver(model, booleanResult).ToArray(), LazyThreadSafetyMode.None);

        var assertions = new Lazy<string[]>(() =>
            metadata.Value
                .ElseFallback(() => Description.ToReason(booleanResult.Satisfied))
                .ToArray(), LazyThreadSafetyMode.None);

        return new BooleanResultWithUnderlying<string,TUnderlyingMetadata>(
            booleanResult,
            () => new MetadataNode<string>(metadata.Value,
                booleanResults as IEnumerable<BooleanResultBase<string>> ?? []),
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
