using System.Threading;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition;

/// <summary>
/// Represents a predicate that when evaluated returns a boolean result with associated metadata and description
/// of the underlying proposition that were responsible for the result.
/// </summary>
/// <typeparam name="TModel">The type of the input parameter.</typeparam>
/// <returns>The return value.</returns>
internal sealed class MultiAssertionExplanationProposition<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, IEnumerable<string>> whenTrue,
    Func<TModel, IEnumerable<string>> whenFalse,
    ISpecDescription specDescription)
    : SpecBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    /// <summary>Gets or sets the description of the proposition.</summary>
    public override ISpecDescription Description => specDescription;

    public override bool Matches(TModel model) => predicate(model);

    /// <summary>Determines if the specified model satisfies the proposition.</summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>A BooleanResultBase object containing the result of the evaluation.</returns>
    protected override BooleanResultBase<string> EvaluateSpec(TModel model)
    {
        var isSatisfied = predicate(model);

        var metadataResolver =
            isSatisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        var metadataNode = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(
                metadataResolver(model),
                []), LazyThreadSafetyMode.None);

        return new PropositionBooleanResult<string>(
            isSatisfied,
            () => metadataNode.Value,
            () => new Explanation(
                metadataNode.Value.Metadata.ElseFallback(() => Description.ToReason(isSatisfied))),
            () => new PropositionResultDescription(
                Description.ToReason(isSatisfied),
                Description.Statement));
    }
}
