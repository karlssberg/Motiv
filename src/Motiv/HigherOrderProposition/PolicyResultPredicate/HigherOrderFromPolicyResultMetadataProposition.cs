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

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);
        var metadataResolver = isSatisfied
            ? whenTrue
            : whenFalse;

        var causes = new Lazy<IReadOnlyList<PolicyResult<TModel, TUnderlyingMetadata>>>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray(), LazyThreadSafetyMode.None);
        var metadata = new Lazy<TMetadata>(() =>
            {
                var evaluation = new HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>(
                    underlyingResults,
                    causes.Value);

                return metadataResolver(evaluation);
            }, LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            metadata.Value switch
            {
                string reasons => reasons,
                _ => specDescription.ToReason(isSatisfied)
            }, LazyThreadSafetyMode.None);

        var reason = new Lazy<string>(() =>
            metadata.Value switch
            {
                string reasons when !Description.HasExplicitStatement => reasons,
                _ => specDescription.ToReason(isSatisfied)
            }, LazyThreadSafetyMode.None);

        var lazyDescription = new Lazy<ResultDescriptionBase>(() =>
            new HigherOrderResultDescription<TUnderlyingMetadata>(
                reason.Value,
                causes.Value,
                Description.Statement), LazyThreadSafetyMode.None);

        var metadataAsEnumerable = new Lazy<IEnumerable<TMetadata>>(() => metadata.Value.ToEnumerable(), LazyThreadSafetyMode.None);
        var assertionAsEnumerable = new Lazy<IEnumerable<string>>(() => assertion.Value.ToEnumerable(), LazyThreadSafetyMode.None);
        var causesAsUnderlying = new Lazy<IEnumerable<BooleanResultBase<TUnderlyingMetadata>>>(() => causes.Value, LazyThreadSafetyMode.None);

        return new HigherOrderPolicyResult<TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            metadata,
            metadataAsEnumerable,
            assertionAsEnumerable,
            lazyDescription,
            underlyingResults,
            causesAsUnderlying);
    }

    private (PolicyResult<TModel, TUnderlyingMetadata>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new PolicyResult<TModel, TUnderlyingMetadata>(model, resultResolver(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
