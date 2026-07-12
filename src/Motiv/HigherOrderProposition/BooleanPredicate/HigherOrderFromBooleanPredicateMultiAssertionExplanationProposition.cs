namespace Motiv.HigherOrderProposition.BooleanPredicate;

internal sealed class HigherOrderFromBooleanPredicateMultiAssertionExplanationProposition<TModel>(
    Func<TModel, bool> predicate,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> whenTrue,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector,
    HigherOrderShortCircuit? shortCircuit = null)
    : SpecBase<IEnumerable<TModel>, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        shortCircuit is { } sc
            ? sc.Evaluate(models, predicate, static (m, p) => p(m))
            : EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<string> EvaluateSpec(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        return new HigherOrderFromBooleanPredicateMultiAssertionExplanationBooleanResult<TModel>(
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
