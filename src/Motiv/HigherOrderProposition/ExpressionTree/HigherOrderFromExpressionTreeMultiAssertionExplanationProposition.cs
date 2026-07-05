using System.Linq.Expressions;
using System.Threading;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.ExpressionTree;

internal sealed class HigherOrderFromExpressionTreeMultiAssertionExplanationProposition<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> trueBecause,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> falseBecause,
    ISpecDescription description,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, string>
{
    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => description;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<string> EvaluateSpec(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);
        var causes = new Lazy<BooleanResult<TModel, string>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray(), LazyThreadSafetyMode.None);

        var because = new Lazy<IEnumerable<string>>(() =>
            {
                var evaluation = new HigherOrderBooleanResultEvaluation<TModel, string>(
                    underlyingResults,
                    causes.Value);

                return isSatisfied
                    ? trueBecause(evaluation)
                    : falseBecause(evaluation);
            }, LazyThreadSafetyMode.None);

        var assertion = new Lazy<IEnumerable<string>>(() =>
            because.Value.ElseFallback(() => underlyingResults.GetAssertions()), LazyThreadSafetyMode.None);

        return new HigherOrderBooleanResult<string, string>(
            isSatisfied,
            () => because.Value,
            () => assertion.Value,
            () => new HigherOrderExpressionTreeResultDescription<string>(
                isSatisfied,
                Description.ToReason(isSatisfied),
                expression,
                causes.Value,
                Description.Statement),
            underlyingResults,
            () => causes.Value);
    }

    private (BooleanResult<TModel, string>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new BooleanResult<TModel, string>(model, _predicate.Execute(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
