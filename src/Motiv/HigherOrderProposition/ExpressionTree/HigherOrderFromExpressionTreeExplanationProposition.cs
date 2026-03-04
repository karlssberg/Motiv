using System.Linq.Expressions;
using System.Threading;
using Motiv.ExpressionTreeProposition;

namespace Motiv.HigherOrderProposition.ExpressionTree;

internal sealed class HigherOrderFromExpressionTreeExplanationProposition<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> trueBecause,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> falseBecause,
    ISpecDescription description,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>,
        IEnumerable<BooleanResult<TModel, string>>> causeSelector)
    : PolicyBase<IEnumerable<TModel>, string>
{
    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => description;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        var causes = new Lazy<BooleanResult<TModel, string>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray(), LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            {
                var booleanCollectionResults = new HigherOrderBooleanResultEvaluation<TModel, string>(
                    underlyingResults,
                    causes.Value);

                return isSatisfied
                    ? trueBecause(booleanCollectionResults)
                    : falseBecause(booleanCollectionResults);
            }, LazyThreadSafetyMode.None);

        var reason = new Lazy<string>(() =>
            description.HasExplicitStatement
                ? description.ToReason(isSatisfied)
                : assertion.Value, LazyThreadSafetyMode.None);

        return new HigherOrderPolicyResult<string, string>(
            isSatisfied,
            () => assertion.Value,
            () => assertion.Value.ToEnumerable(),
            () => assertion.Value.ToEnumerable(),
            () => new HigherOrderExpressionTreeResultDescription<string>(
                isSatisfied,
                reason.Value,
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
