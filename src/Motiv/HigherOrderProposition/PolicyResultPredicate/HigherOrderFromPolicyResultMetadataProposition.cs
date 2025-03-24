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
        var underlyingResults = models
            .Select(model => new PolicyResult<TModel, TUnderlyingMetadata>(model,  resultResolver(model)))
            .ToArray();
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var metadataResolver = isSatisfied
            ? whenTrue
            : whenFalse;

        var causes = new Lazy<IReadOnlyList<PolicyResult<TModel, TUnderlyingMetadata>>>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());
        var metadata = new Lazy<TMetadata>(() =>
        {
            var evaluation = new HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>(
                underlyingResults,
                causes.Value);

            return metadataResolver(evaluation);
        });

        var assertion = new Lazy<string>(() =>
            metadata.Value switch
            {
                string reasons => reasons,
                _ => specDescription.ToReason(isSatisfied)
            });

        var lazyDescription = new Lazy<ResultDescriptionBase>(() =>
            new HigherOrderResultDescription<TUnderlyingMetadata>(
                assertion.Value,
                [],
                causes.Value,
                Description.Statement));

        return new HigherOrderPolicyResult<TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            () => metadata.Value,
            () => metadata.Value.ToEnumerable(),
            () => assertion.Value.ToEnumerable(),
            () => lazyDescription.Value,
            underlyingResults,
            () => causes.Value);
    }
}
