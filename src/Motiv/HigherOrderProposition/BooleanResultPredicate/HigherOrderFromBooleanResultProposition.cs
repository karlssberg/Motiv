namespace Motiv.HigherOrderProposition.BooleanResultPredicate;

internal sealed class HigherOrderFromBooleanResultProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : PolicyBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        return new HigherOrderFromBooleanResultPolicyResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            underlyingResults,
            whenTrue,
            whenFalse,
            specDescription,
            causeSelector);
    }

    private (BooleanResult<TModel, TUnderlyingMetadata>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = HigherOrderResults.Materialize(models, resultResolver,
            static (model, resolve) => new BooleanResult<TModel, TUnderlyingMetadata>(model, resolve(model)));
        return (results, higherOrderPredicate(results));
    }
}
