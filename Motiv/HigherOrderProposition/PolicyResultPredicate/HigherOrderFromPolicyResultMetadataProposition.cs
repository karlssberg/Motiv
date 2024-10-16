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

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = EvaluateModels(models);
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = GetLazyCauses(isSatisfied, underlyingResults);
        var metadata = GetLazyMetadata(underlyingResults, causes, isSatisfied);

        return CreatePolicyResult(
            metadata,
            isSatisfied,
            underlyingResults,
            causes);
    }

    private PolicyResult<TModel, TUnderlyingMetadata>[] EvaluateModels(IEnumerable<TModel> models) =>
        models
            .Select(model => new PolicyResult<TModel, TUnderlyingMetadata>(model,  resultResolver(model)))
            .ToArray();

    private Lazy<IReadOnlyList<PolicyResult<TModel, TUnderlyingMetadata>>> GetLazyCauses(
        bool isSatisfied,
        IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> underlyingResults) =>
        new(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());

    private Lazy<TMetadata> GetLazyMetadata(
        IReadOnlyList<PolicyResult<TModel, TUnderlyingMetadata>> underlyingResults,
        Lazy<IReadOnlyList<PolicyResult<TModel, TUnderlyingMetadata>>> causes,
        bool isSatisfied)
    {
        return new Lazy<TMetadata>(() =>
        {
            var evaluation = new HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>(
                underlyingResults,
                causes.Value);

            return isSatisfied
                ? whenTrue(evaluation)
                : whenFalse(evaluation);
        });
    }

    private PolicyResultBase<TMetadata> CreatePolicyResult(
        Lazy<TMetadata> metadata,
        bool isSatisfied,
        IEnumerable<BooleanResultBase<TUnderlyingMetadata>> underlyingResults,
        Lazy<IReadOnlyList<PolicyResult<TModel, TUnderlyingMetadata>>> causes)
    {
        var assertion = new Lazy<string>(() =>
            metadata.Value switch
            {
                string reasons => reasons,
                _ => specDescription.ToReason(isSatisfied)
            });


        var lazyDescription = new Lazy<ResultDescriptionBase>(() =>
            new HigherOrderResultDescription<TUnderlyingMetadata>(
                specDescription.ToReason(isSatisfied),
                [],
                causes.Value,
                Description.Statement));

        return new HigherOrderPolicyResult<TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            Value,
            Metadata,
            Assertions,
            ResultDescription,
            underlyingResults,
            GetCauses);

        TMetadata Value() => metadata.Value;
        IEnumerable<TMetadata> Metadata() => metadata.Value.ToEnumerable();
        IEnumerable<string> Assertions() => assertion.Value.ToEnumerable();
        ResultDescriptionBase ResultDescription() => lazyDescription.Value;
        IEnumerable<BooleanResultBase<TUnderlyingMetadata>> GetCauses() => causes.Value;
    }
}
