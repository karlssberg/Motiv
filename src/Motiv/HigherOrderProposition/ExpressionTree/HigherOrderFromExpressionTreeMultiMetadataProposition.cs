using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.HigherOrderProposition.ExpressionTree;

internal sealed class HigherOrderFromExpressionTreeMultiMetadataProposition<TModel, TMetadata, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription description,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => description;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new BooleanResult<TModel, string>(model,  _predicate.Execute(model)))
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
                _ => underlyingResults.GetAssertions()
            });

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new HigherOrderExpressionTreeResultDescription<string>(
                isSatisfied,
                Description.ToReason(isSatisfied),
                typeof(TMetadata) == typeof(string) ? assertions.Value : [],
                expression,
                causes.Value,
                Description.Statement));

        return new HigherOrderBooleanResult<TMetadata, string>(
            isSatisfied,
            () => metadata.Value,
            () => assertions.Value,
            () => resultDescription.Value,
            underlyingResults,
            () => causes.Value);
    }
}
