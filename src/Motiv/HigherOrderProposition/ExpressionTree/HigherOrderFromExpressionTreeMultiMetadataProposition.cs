using System.Linq.Expressions;
using System.Threading;
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

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);
        var causes = new Lazy<BooleanResult<TModel, string>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray(), LazyThreadSafetyMode.None);

        var metadata = new Lazy<IEnumerable<TMetadata>>(() =>
            {
                var evaluation = new HigherOrderBooleanResultEvaluation<TModel, string>(
                    underlyingResults,
                    causes.Value);

                return isSatisfied
                    ? whenTrue(evaluation)
                    : whenFalse(evaluation);
            }, LazyThreadSafetyMode.None);

        var assertions = new Lazy<IEnumerable<string>>(() =>
            metadata.Value switch
            {
                IEnumerable<string> reasons => reasons,
                _ => underlyingResults.GetAssertions()
            }, LazyThreadSafetyMode.None);

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new HigherOrderExpressionTreeResultDescription<string>(
                isSatisfied,
                Description.ToReason(isSatisfied),
                expression,
                causes.Value,
                Description.Statement), LazyThreadSafetyMode.None);

        var causesAsUnderlying = new Lazy<IEnumerable<BooleanResultBase<string>>>(() => causes.Value, LazyThreadSafetyMode.None);

        return new HigherOrderBooleanResult<TMetadata, string>(
            isSatisfied,
            metadata,
            assertions,
            resultDescription,
            underlyingResults,
            causesAsUnderlying);
    }

    private (BooleanResult<TModel, string>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new BooleanResult<TModel, string>(model, _predicate.Execute(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
