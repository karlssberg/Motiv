namespace Motiv.HigherOrderProposition.BooleanResultPredicate;

internal sealed class MinimalHigherOrderFromBooleanResultProposition<TModel, TMetadata>(
    Func<TModel, BooleanResultBase<TMetadata>> resultResolver,
    Func<IEnumerable<BooleanResult<TModel, TMetadata>>, bool> higherOrderPredicate,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TMetadata>>, IEnumerable<BooleanResult<TModel, TMetadata>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);
        var causes = new Lazy<BooleanResult<TModel, TMetadata>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray(), LazyThreadSafetyMode.None);

        var metadata = new Lazy<IEnumerable<TMetadata>>(() =>
            {
                var evaluation = new HigherOrderBooleanResultEvaluation<TModel, TMetadata>(
                    underlyingResults,
                    causes.Value);

                return evaluation.Values;
            }, LazyThreadSafetyMode.None);

        return new HigherOrderBooleanResult<TMetadata, TMetadata>(
            isSatisfied,
            () => metadata.Value,
            () => metadata.Value switch
            {
                IEnumerable<string> reasons => reasons,
                _ => specDescription.ToReason(isSatisfied).ToEnumerable()
            },
            () => new HigherOrderResultDescription<TMetadata>(
                specDescription.ToReason(isSatisfied),
                causes.Value,
                Description.Statement),
            underlyingResults,
            () => causes.Value);
    }

    private (BooleanResult<TModel, TMetadata>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new BooleanResult<TModel, TMetadata>(model, resultResolver(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
