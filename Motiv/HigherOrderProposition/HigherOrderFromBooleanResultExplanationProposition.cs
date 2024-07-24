namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanResultExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, string> falseBecause,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>,
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
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

    private BooleanResult<TModel, TUnderlyingMetadata>[] EvaluateModels(IEnumerable<TModel> models)
    {
        return models
            .Select(model => new BooleanResult<TModel, TUnderlyingMetadata>(model, resultResolver(model)))
            .ToArray();
    }

    private Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]> DetermineCauses(bool isSatisfied,
        BooleanResult<TModel, TUnderlyingMetadata>[] underlyingResults)
    {
        return new Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());
    }

    private Lazy<string> GetLazyAssertion(BooleanResult<TModel, TUnderlyingMetadata>[] underlyingResults,
        Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]> causes, bool isSatisfied)
    {
        return new Lazy<string>(() =>
        {
            var booleanCollectionResults = new HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>(
                underlyingResults,
                causes.Value);

            return isSatisfied
                ? trueBecause(booleanCollectionResults)
                : falseBecause(booleanCollectionResults);
        });
    }

    private PolicyResultBase<string> CreatePolicyResult(bool isSatisfied,
        BooleanResult<TModel, TUnderlyingMetadata>[] underlyingResults,
        Lazy<string> assertion, Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]> causes)
    {
        var lazyDescription = new Lazy<ResultDescriptionBase>(() =>
            new HigherOrderResultDescription<TUnderlyingMetadata>(
                assertion.Value,
                causes.Value,
                Description.Statement));

        return new HigherOrderPolicyResult<string, TUnderlyingMetadata>(
            isSatisfied,
            Value,
            Metadata,
            Assertions,
            ResultDescription,
            underlyingResults,
            GetCauses);

        string Value() => assertion.Value;
        IEnumerable<string> Metadata() => assertion.Value.ToEnumerable();
        IEnumerable<string> Assertions() => assertion.Value.ToEnumerable();
        ResultDescriptionBase ResultDescription() => lazyDescription.Value;
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> GetCauses() => causes.Value;
    }
}
