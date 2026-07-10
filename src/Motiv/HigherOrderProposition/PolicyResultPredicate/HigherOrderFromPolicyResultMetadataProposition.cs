namespace Motiv.HigherOrderProposition.PolicyResultPredicate;

internal sealed class HigherOrderFromPolicyResultMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : PolicyBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        return new HigherOrderFromPolicyResultMetadataPolicyResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            underlyingResults,
            whenTrue,
            whenFalse,
            specDescription,
            causeSelector);
    }

    private (PolicyResult<TModel, TUnderlyingMetadata>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = HigherOrderResults.Materialize(models, resultResolver,
            static (model, resolve) => new PolicyResult<TModel, TUnderlyingMetadata>(model, resolve(model)));
        return (results, higherOrderPredicate(results));
    }
}
