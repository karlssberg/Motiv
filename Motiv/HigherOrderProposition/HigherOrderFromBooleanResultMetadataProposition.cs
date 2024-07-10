namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanResultMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : PolicyBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];


    public override ISpecDescription Description => specDescription;

    public override PolicyResultBase<TMetadata> Execute(IEnumerable<TModel> models)
    {
        var underlyingResults = EvaluateModels(models);
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = GetLazyCauses(isSatisfied, underlyingResults);
        var metadata = GetLazyMetadata(underlyingResults, causes, isSatisfied);

        return CreatePolicyResult(metadata, isSatisfied, underlyingResults, causes);
    }

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models) =>
        Execute(models);

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
            var evaluation = new HigherOrderEvaluation<TModel, TUnderlyingMetadata>(
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

        return new HigherOrderPolicyResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            Value,
            Metadata,
            Assertions,
            Reason,
            underlyingResults,
            GetCauses);

        TMetadata Value() => metadata.Value;
        IEnumerable<TMetadata> Metadata() => metadata.Value.ToEnumerable();
        IEnumerable<string> Assertions() => assertions.Value;
        string Reason() => specDescription.ToReason(isSatisfied);
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> GetCauses() => causes.Value;
    }
}
