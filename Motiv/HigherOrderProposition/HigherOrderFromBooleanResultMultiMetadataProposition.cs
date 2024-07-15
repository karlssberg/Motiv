namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanResultMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new BooleanResult<TModel, TUnderlyingMetadata>(model,  resultResolver(model)))
            .ToArray();

        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = new Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());

        var metadata = new Lazy<IEnumerable<TMetadata>>(() =>
        {
            var evaluation = new HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>(
                underlyingResults,
                causes.Value);

            return isSatisfied
                ? whenTrue(evaluation)
                : whenFalse(evaluation);
        });

        var assertions = new Lazy<IEnumerable<string>>(() =>
            metadata.Value switch
            {
                IEnumerable<string>  reasons => reasons,
                _ => specDescription.ToReason(isSatisfied).ToEnumerable()
            });

        return new HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            Metadata,
            Assertions,
            Reason,
            underlyingResults,
            GetCauses);

        IEnumerable<TMetadata> Metadata() => metadata.Value;
        IEnumerable<string> Assertions() => assertions.Value;
        string Reason() => specDescription.ToReason(isSatisfied);
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> GetCauses() => causes.Value;
    }
}

internal sealed class HigherOrderFromPolicyResultMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new PolicyResult<TModel, TUnderlyingMetadata>(model,  resultResolver(model)))
            .ToArray();

        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = new Lazy<PolicyResult<TModel, TUnderlyingMetadata>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());

        var metadata = new Lazy<IEnumerable<TMetadata>>(() =>
        {
            var evaluation = new HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>(
                underlyingResults,
                causes.Value);

            return isSatisfied
                ? whenTrue(evaluation)
                : whenFalse(evaluation);
        });

        var assertions = new Lazy<IEnumerable<string>>(() =>
            metadata.Value switch
            {
                IEnumerable<string>  reasons => reasons,
                _ => specDescription.ToReason(isSatisfied).ToEnumerable()
            });

        return new HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            Metadata,
            Assertions,
            Reason,
            underlyingResults.OfType<BooleanResultBase<TUnderlyingMetadata>>(),
            GetCauses);

        IEnumerable<TMetadata> Metadata() => metadata.Value;
        IEnumerable<string> Assertions() => assertions.Value;
        string Reason() => specDescription.ToReason(isSatisfied);
        IEnumerable<BooleanResultBase<TUnderlyingMetadata>> GetCauses() => causes.Value;
    }
}
