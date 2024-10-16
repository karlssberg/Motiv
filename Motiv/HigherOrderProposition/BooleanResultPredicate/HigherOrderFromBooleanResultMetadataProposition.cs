namespace Motiv.HigherOrderProposition.BooleanResultPredicate;

internal sealed class HigherOrderFromBooleanResultMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
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
        var underlyingResults = EvaluateModels(models);
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = GetLazyCauses(isSatisfied, underlyingResults);
        var metadata = GetLazyMetadata(underlyingResults, causes, isSatisfied);

        return CreatePolicyResult(metadata, isSatisfied, underlyingResults, causes);
    }

    private BooleanResult<TModel, TUnderlyingMetadata>[] EvaluateModels(IEnumerable<TModel> models) =>
        models
            .Select(model => new BooleanResult<TModel, TUnderlyingMetadata>(model,  resultResolver(model)))
            .ToArray();

    private Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]> GetLazyCauses(
        bool isSatisfied,
        BooleanResult<TModel, TUnderlyingMetadata>[] underlyingResults) =>
        new(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());

    private Lazy<TMetadata> GetLazyMetadata(
        BooleanResult<TModel, TUnderlyingMetadata>[] underlyingResults,
        Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]> causes,
        bool isSatisfied)
    {
        return new Lazy<TMetadata>(() =>
        {
            var evaluation = new HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>(
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
        BooleanResult<TModel, TUnderlyingMetadata>[] underlyingResults,
        Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]> causes)
    {
        var assertions = new Lazy<IEnumerable<string>>(() =>
            metadata.Value switch
            {
                IEnumerable<string>  reasons => reasons,
                _ => specDescription.ToReason(isSatisfied).ToEnumerable()
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
        IEnumerable<string> Assertions() => assertions.Value;
        ResultDescriptionBase ResultDescription() => lazyDescription.Value;
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> GetCauses() => causes.Value;
    }
}
