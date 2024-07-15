namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromPolicyResultExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, string> falseBecause,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>,
        IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : PolicyBase<IEnumerable<TModel>, string>
{
    public override IEnumerable<SpecBase> Underlying => [];


    public override ISpecDescription Description => specDescription;

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = EvaluateModels(models);

        var isSatisfied = higherOrderPredicate(underlyingResults);

        var causes = DetermineCauses(isSatisfied, underlyingResults);

        var assertion = GetLazyAssertion(underlyingResults, causes, isSatisfied);

        return CreatePolicyResult(isSatisfied, underlyingResults, assertion, causes);
    }

    private PolicyResult<TModel, TUnderlyingMetadata>[] EvaluateModels(IEnumerable<TModel> models)
    {
        return models
            .Select(model => new PolicyResult<TModel, TUnderlyingMetadata>(model, resultResolver(model)))
            .ToArray();
    }

    private Lazy<PolicyResult<TModel, TUnderlyingMetadata>[]> DetermineCauses(bool isSatisfied,
        PolicyResult<TModel, TUnderlyingMetadata>[] underlyingResults)
    {
        return new Lazy<PolicyResult<TModel, TUnderlyingMetadata>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());
    }

    private Lazy<string> GetLazyAssertion(PolicyResult<TModel, TUnderlyingMetadata>[] underlyingResults,
        Lazy<PolicyResult<TModel, TUnderlyingMetadata>[]> causes, bool isSatisfied)
    {
        return new Lazy<string>(() =>
        {
            var booleanCollectionResults = new HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>(
                underlyingResults,
                causes.Value);

            return isSatisfied
                ? trueBecause(booleanCollectionResults)
                : falseBecause(booleanCollectionResults);
        });
    }

    private static PolicyResultBase<string> CreatePolicyResult(bool isSatisfied,
        PolicyResult<TModel, TUnderlyingMetadata>[] underlyingResults,
        Lazy<string> assertion, Lazy<PolicyResult<TModel, TUnderlyingMetadata>[]> causes)
    {
        return new HigherOrderPolicyResult<string, TUnderlyingMetadata>(
            isSatisfied,
            Value,
            Metadata,
            Assertions,
            Reason,
            underlyingResults,
            GetCauses);

        string Value() => assertion.Value;
        IEnumerable<string> Metadata() => assertion.Value.ToEnumerable();
        IEnumerable<string> Assertions() => assertion.Value.ToEnumerable();
        string Reason() => assertion.Value;
        IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> GetCauses() => causes.Value;
    }
}
