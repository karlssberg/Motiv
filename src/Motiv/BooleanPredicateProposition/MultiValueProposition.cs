using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition;

/// <summary>
/// Represents a predicate that when evaluated returns a boolean result with associated metadata and description
/// of the underlying proposition that were responsible for the result.
/// </summary>
/// <typeparam name="TModel">The type of the input parameter.</typeparam>
/// <typeparam name="TMetadata">The type of the return value.</typeparam>
/// <returns>The return value.</returns>
internal sealed class MultiValueProposition<TModel, TMetadata>(
    Func<TModel, bool> predicate,
    Func<TModel, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription)
    : SpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    /// <summary>Gets or sets the description of the proposition.</summary>
    public override ISpecDescription Description => specDescription;

    /// <summary>Determines if the specified model satisfies the proposition.</summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>A BooleanResultBase object containing the result of the evaluation.</returns>
    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model)
    {
        var isSatisfied = predicate(model);

        var metadata = isSatisfied switch
        {
            true => whenTrue(model),
            false => whenFalse(model)
        };

        var assertion = metadata switch
        {
            IEnumerable<string> because => because,
            _ => Description.ToReason(isSatisfied).ToEnumerable()
        };

        return new PropositionBooleanResult<TMetadata>(
            isSatisfied,
            new Lazy<MetadataNode<TMetadata>>(() => new MetadataNode<TMetadata>(metadata, [])),
            new Lazy<Explanation>(() => new Explanation(assertion)),
            new Lazy<ResultDescriptionBase>(() =>
                new PropositionResultDescription(Description.ToReason(isSatisfied), Description.Statement)));
    }
}
