namespace Motiv.HigherOrderProposition.BooleanResultPredicate;

internal sealed class HigherOrderFromBooleanResultMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<TMetadata> EvaluateSpec(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);
        var causes = new Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray(), LazyThreadSafetyMode.None);

        var metadata = new Lazy<IEnumerable<TMetadata>>(() =>
            {
                var evaluation = new HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>(
                    underlyingResults,
                    causes.Value);

                return isSatisfied
                    ? whenTrue(evaluation)
                    : whenFalse(evaluation);
            }, LazyThreadSafetyMode.None);

        return new HigherOrderBooleanResult<TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            () => metadata.Value,
            () => specDescription.ToReason(isSatisfied).ToEnumerable(),
            () => new HigherOrderResultDescription<TUnderlyingMetadata>(
                specDescription.ToReason(isSatisfied),
                causes.Value,
                Description.Statement),
            underlyingResults,
            () => causes.Value);
    }

    private (BooleanResult<TModel, TUnderlyingMetadata>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new BooleanResult<TModel, TUnderlyingMetadata>(model, resultResolver(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
