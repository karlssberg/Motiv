using System.Linq.Expressions;

namespace Motiv.HigherOrderProposition.ExpressionTree;

internal sealed class HigherOrderFromBooleanResultMultiMetadataExpressionTreeProposition<TModel, TMetadata>(
    Expression<Func<TModel, bool>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    private readonly SpecBase<TModel, string> _spec = expression.ToSpec();

    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new BooleanResult<TModel, string>(model,  _spec.IsSatisfiedBy(model)))
            .ToArray();

        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = new Lazy<BooleanResult<TModel, string>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());

        var metadata = new Lazy<IEnumerable<TMetadata>>(() =>
        {
            var evaluation = new HigherOrderBooleanResultEvaluation<TModel, string>(
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

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
                new HigherOrderResultDescription<string>(
                    specDescription.ToReason(isSatisfied),
                    typeof(TMetadata) == typeof(string) ? assertions.Value : [],
                    causes.Value,
                    Description.Statement));

        return new HigherOrderBooleanResult<TMetadata, string>(
            isSatisfied,
            Metadata,
            Assertions,
            ResultDescription,
            underlyingResults,
            GetCauses);

        IEnumerable<TMetadata> Metadata() => metadata.Value;
        IEnumerable<string> Assertions() => assertions.Value;
        IEnumerable<BooleanResult<TModel, string>> GetCauses() => causes.Value;
        ResultDescriptionBase ResultDescription() => resultDescription.Value;
    }
}
