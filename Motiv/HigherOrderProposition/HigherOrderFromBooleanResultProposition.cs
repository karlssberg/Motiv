namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanResultProposition<TModel, TMetadata>(
    Func<TModel, BooleanResultBase<TMetadata>> resultResolver,
    Func<IEnumerable<BooleanResult<TModel, TMetadata>>, bool> higherOrderPredicate,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TMetadata>>, IEnumerable<BooleanResult<TModel, TMetadata>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new BooleanResult<TModel, TMetadata>(model,  resultResolver(model)))
            .ToArray();

        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = new Lazy<BooleanResult<TModel, TMetadata>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());

        var metadata = new Lazy<IEnumerable<TMetadata>>(() =>
        {
            var evaluation = new HigherOrderBooleanResultEvaluation<TModel, TMetadata>(
                underlyingResults,
                causes.Value);

            return evaluation.Values;
        });

        var assertions = new Lazy<IEnumerable<string>>(() =>
            metadata.Value switch
            {
                IEnumerable<string>  reasons => reasons,
                _ => specDescription.ToReason(isSatisfied).ToEnumerable()
            });

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
                new HigherOrderResultDescription<TMetadata>(
                    specDescription.ToReason(isSatisfied),
                    [],
                    causes.Value,
                    Description.Statement));

        return new HigherOrderBooleanResult<TMetadata, TMetadata>(
            isSatisfied,
            Metadata,
            Assertions,
            ResultDescription,
            underlyingResults,
            GetCauses);

        IEnumerable<TMetadata> Metadata() => metadata.Value;
        IEnumerable<string> Assertions() => assertions.Value;
        IEnumerable<BooleanResult<TModel, TMetadata>> GetCauses() => causes.Value;
        ResultDescriptionBase ResultDescription() => resultDescription.Value;
    }
}
