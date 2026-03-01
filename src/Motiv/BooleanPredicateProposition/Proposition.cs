using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition;

/// <summary>
/// Represents a predicate that when evaluated returns a boolean result with associated metadata and description
/// of the underlying proposition that were responsible for the result.
/// </summary>
/// <typeparam name="TModel">The type of the input parameter.</typeparam>
/// <typeparam name="TMetadata">The type of the return value.</typeparam>
/// <returns>The return value.</returns>
internal sealed class Proposition<TModel, TMetadata>(
    Func<TModel, bool> predicate,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse,
    ISpecDescription specDescription)
    : PolicyBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    /// <summary>Gets or sets the description of the proposition.</summary>
    public override ISpecDescription Description => specDescription;

    public override bool Matches(TModel model) => predicate(model);

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var isSatisfied = predicate(model);

        var metadataResolver =
            isSatisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        var metadata = new Lazy<TMetadata>(() => metadataResolver(model));

        var assertion = new Lazy<string> (() =>
            metadata.Value switch
            {
                string because => because,
                _ => Description.ToReason(isSatisfied)
            });

        var reason = new Lazy<string> (() =>
            metadata.Value switch
            {
                string because => because,
                _ => Description.ToReason(isSatisfied)
            });

        return new PropositionPolicyResult<TMetadata>(
            isSatisfied,
            metadata,
            new Lazy<MetadataNode<TMetadata>>(() => new MetadataNode<TMetadata>(metadata.Value, [])),
            new Lazy<Explanation>(() => new Explanation(assertion.Value)),
            new Lazy<ResultDescriptionBase>(() =>
                new PropositionResultDescription(reason.Value, Description.Statement)));
    }
}
