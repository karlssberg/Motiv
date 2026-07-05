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

    protected override BooleanResultBase<TMetadata> EvaluateSpec(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        return new MinimalHigherOrderFromBooleanResultBooleanResult<TModel, TMetadata>(
            isSatisfied,
            underlyingResults,
            specDescription,
            causeSelector);
    }

    private (BooleanResult<TModel, TMetadata>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new BooleanResult<TModel, TMetadata>(model, resultResolver(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
