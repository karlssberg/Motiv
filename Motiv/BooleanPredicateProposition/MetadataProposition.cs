using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition;

/// <summary>
/// Represents a predicate that when evaluated returns a boolean result with associated metadata and description
/// of the underlying proposition that were responsible for the result.
/// </summary>
/// <typeparam name="TModel">The type of the input parameter.</typeparam>
/// <typeparam name="TMetadata">The type of the return value.</typeparam>
/// <returns>The return value.</returns>
internal sealed class MetadataProposition<TModel, TMetadata>(
    Func<TModel, bool> predicate,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse,
    ISpecDescription specDescription)
    : PolicyBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    /// <summary>Gets or sets the description of the proposition.</summary>
    public override ISpecDescription Description => specDescription;

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var isSatisfied = InvokePredicate(model);

        var metadata = ResolveMetadata(model, isSatisfied);

        return CreatePolicyResult(metadata, isSatisfied);
    }

    private Lazy<TMetadata> ResolveMetadata(TModel model, bool isSatisfied)
    {
        return new Lazy<TMetadata>(() => isSatisfied switch
        {
            true => InvokeWhenTrueFunction(model),
            false => InvokeWhenFalseFunction(model)
        });
    }

    private PolicyResultBase<TMetadata> CreatePolicyResult(Lazy<TMetadata> metadata, bool isSatisfied)
    {
        var assertion = new Lazy<IEnumerable<string>> (() => metadata.Value switch
        {
            IEnumerable<string> because => because,
            _ => Description.ToReason(isSatisfied).ToEnumerable()
        });

        return new PropositionPolicyResult<TMetadata>(
            isSatisfied,
            metadata,
            new Lazy<MetadataNode<TMetadata>>(() => new MetadataNode<TMetadata>(metadata.Value, [])),
            new Lazy<Explanation>(() => new Explanation(assertion.Value, [], [])),
            new Lazy<ResultDescriptionBase>(() =>
                new PropositionResultDescription(Description.ToReason(isSatisfied), Description.Statement)));
    }

    private bool InvokePredicate(TModel model) =>
        WrapException.CatchFuncExceptionOnBehalfOfSpecType(
            this,
            () => predicate(model),
            nameof(predicate));

    private TMetadata InvokeWhenTrueFunction(TModel model) =>
        WrapException.CatchFuncExceptionOnBehalfOfSpecType(
            this,
            () => whenTrue(model),
            nameof(whenTrue));

    private TMetadata InvokeWhenFalseFunction(TModel model) =>
        WrapException.CatchFuncExceptionOnBehalfOfSpecType(
            this,
            () => whenFalse(model),
            nameof(whenFalse));
}
