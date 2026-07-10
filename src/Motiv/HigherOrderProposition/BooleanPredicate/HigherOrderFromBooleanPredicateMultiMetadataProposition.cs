namespace Motiv.HigherOrderProposition.BooleanPredicate;

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

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<TMetadata> EvaluateSpec(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        return new HigherOrderFromBooleanPredicateMultiMetadataBooleanResult<TModel, TMetadata>(
            isSatisfied,
            underlyingResults,
            whenTrue,
            whenFalse,
            specDescription,
            causeSelector);
    }

    private (ModelResult<TModel>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = HigherOrderResults.Materialize(models, predicate,
            static (model, p) => new ModelResult<TModel>(model, p(model)));
        return (results, higherOrderPredicate(results));
    }
}
