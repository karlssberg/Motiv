namespace Motiv.HigherOrderProposition.PolicyResultPredicate;

internal sealed class MinimalHigherOrderFromPolicyResultProposition<TModel, TMetadata>(
    Func<TModel, PolicyResultBase<TMetadata>> resultResolver,
    Func<IEnumerable<PolicyResult<TModel, TMetadata>>, bool> higherOrderPredicate,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<PolicyResult<TModel, TMetadata>>, IEnumerable<PolicyResult<TModel, TMetadata>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<TMetadata> EvaluateSpec(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        return new MinimalHigherOrderFromPolicyResultBooleanResult<TModel, TMetadata>(
            isSatisfied,
            underlyingResults,
            specDescription,
            causeSelector);
    }

    private (PolicyResult<TModel, TMetadata>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = HigherOrderResults.Materialize(models, resultResolver,
            static (model, resolve) => new PolicyResult<TModel, TMetadata>(model, resolve(model)));
        return (results, higherOrderPredicate(results));
    }
}
