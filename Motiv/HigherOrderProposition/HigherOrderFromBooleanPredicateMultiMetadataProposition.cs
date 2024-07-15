namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanPredicateMultiMetadataProposition<TModel, TMetadata>(
    Func<TModel, bool> predicate,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    internal override BooleanResultBase<TMetadata> IsSatisfiedByInternal(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new ModelResult<TModel>(model, predicate(model)))
            .ToArray();

        var isSatisfied = higherOrderPredicate(underlyingResults);


        var lazyMetadata = new Lazy<IEnumerable<TMetadata>>(() =>
        {
            var causes = causeSelector(isSatisfied, underlyingResults).ToArray();
            var evaluation = new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causes);

            return isSatisfied
                ? whenTrue(evaluation)
                : whenFalse(evaluation);
        });

        var lazyAssertion = new Lazy<IEnumerable<string>>(() =>
            lazyMetadata.Value switch
            {
                IEnumerable<string> because => because,
                _ => specDescription.ToReason(isSatisfied).ToEnumerable()
            });

        var lazyDescription = new Lazy<BooleanResultDescription>(() =>
            new BooleanResultDescription(specDescription.ToReason(isSatisfied), lazyAssertion.Value));

        return new HigherOrderFromBooleanPredicateBooleanResult<TMetadata>(
            isSatisfied,
            Metadata,
            Explanation,
            Description);

        MetadataNode<TMetadata> Metadata() => new(lazyMetadata.Value, []);
        Explanation Explanation() => new(lazyAssertion.Value, []);
        BooleanResultDescription Description() => lazyDescription.Value;
    }
}
