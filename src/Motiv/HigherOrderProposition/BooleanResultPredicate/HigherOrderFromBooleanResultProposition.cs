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

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new BooleanResult<TModel, TUnderlyingMetadata>(model,  resultResolver(model)))
            .ToArray();

        var isSatisfied = higherOrderPredicate(underlyingResults);
        var metadataResolver = isSatisfied
            ? whenTrue
            : whenFalse;

        var causes = new Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());

        var metadata = new Lazy<TMetadata>(() =>
        {
            var evaluation = new HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>(
                underlyingResults,
                causes.Value);

            return metadataResolver(evaluation);
        });

        var assertion = new Lazy<string>(() =>
            metadata.Value switch
            {
                string  reasons => reasons,
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
