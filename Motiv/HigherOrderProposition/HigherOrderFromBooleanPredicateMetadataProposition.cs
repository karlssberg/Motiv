namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanPredicateMetadataProposition<TModel, TMetadata>(
    Func<TModel,bool> predicate,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue,
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
    : PolicyBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override PolicyResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = EvaluateModels(models);
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var metadata = GetLazyMetadata(isSatisfied, underlyingResults);

        return CreateBooleanResult(isSatisfied, metadata);
    }

    private ModelResult<TModel>[] EvaluateModels(IEnumerable<TModel> models)
    {
        return models
            .Select(model => new ModelResult<TModel>(model, predicate(model)))
            .ToArray();
    }

    private Lazy<TMetadata> GetLazyMetadata(bool isSatisfied, ModelResult<TModel>[] underlyingResults)
    {
        var metadata = new Lazy<TMetadata>(() =>
        {
            var causes = causeSelector(isSatisfied, underlyingResults).ToArray();
            var evaluation = new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causes);

            return isSatisfied
                ? whenTrue(evaluation)
                : whenFalse(evaluation);
        });
        return metadata;
    }

    private PolicyResultBase<TMetadata> CreateBooleanResult(bool isSatisfied, Lazy<TMetadata> metadata)
    {
        return new HigherOrderFromBooleanPredicatePolicyResult<TMetadata>(
            isSatisfied,
            Value,
            Metadata,
            Explanation,
            Reason);

        TMetadata Value() => metadata.Value;
        MetadataNode<TMetadata> Metadata() => new(metadata.Value, []);
        Explanation Explanation() => new(specDescription.ToReason(isSatisfied), []);
        string Reason() => specDescription.ToReason(isSatisfied);
    }
}
