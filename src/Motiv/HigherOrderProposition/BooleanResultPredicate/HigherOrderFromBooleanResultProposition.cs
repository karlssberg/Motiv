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

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);
        var metadataResolver = isSatisfied
            ? whenTrue
            : whenFalse;

        var causes = new Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray(), LazyThreadSafetyMode.None);

        var metadata = new Lazy<TMetadata>(() =>
            {
                var evaluation = new HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>(
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
            specDescription.HasExplicitStatement
                ? specDescription.ToReason(isSatisfied)
                : assertion.Value, LazyThreadSafetyMode.None);

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

    private (BooleanResult<TModel, TUnderlyingMetadata>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new BooleanResult<TModel, TUnderlyingMetadata>(model, resultResolver(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
