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

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);
        var causes = new Lazy<PolicyResult<TModel, TMetadata>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());

        var metadata = new Lazy<IEnumerable<TMetadata>>(() => causes.Value.Select(result => result.Value).ToArray());

        var assertions = new Lazy<IEnumerable<string>>(() =>
            metadata.Value switch
            {
                IEnumerable<string> reasons => reasons,
                _ => specDescription.ToReason(isSatisfied).ToEnumerable()
            });

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new HigherOrderResultDescription<TMetadata>(
                specDescription.ToReason(isSatisfied),
                causes.Value,
                Description.Statement));

        var causesAsUnderlying = new Lazy<IEnumerable<BooleanResultBase<TMetadata>>>(() => causes.Value);

        return new HigherOrderBooleanResult<TMetadata, TMetadata>(
            isSatisfied,
            metadata,
            assertions,
            resultDescription,
            underlyingResults,
            causesAsUnderlying);
    }

    private (PolicyResult<TModel, TMetadata>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new PolicyResult<TModel, TMetadata>(model, resultResolver(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
