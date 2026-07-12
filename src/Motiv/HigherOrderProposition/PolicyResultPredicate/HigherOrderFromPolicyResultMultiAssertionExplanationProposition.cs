namespace Motiv.HigherOrderProposition.PolicyResultPredicate;

internal sealed class HigherOrderFromPolicyResultMultiAssertionExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> whenTrue,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector,
    HigherOrderShortCircuit? shortCircuit = null)
    : SpecBase<IEnumerable<TModel>, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        shortCircuit is { } sc
            ? sc.Evaluate(models, resultResolver, static (m, r) => r(m).Satisfied)
            : EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<string> EvaluateSpec(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        return new HigherOrderFromPolicyResultMultiAssertionExplanationBooleanResult<TModel, TUnderlyingMetadata>(
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
