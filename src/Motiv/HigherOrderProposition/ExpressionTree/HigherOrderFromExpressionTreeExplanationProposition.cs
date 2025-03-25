using System.Linq.Expressions;
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

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new BooleanResult<TModel, string>(model, _predicate.Execute(model)))
            .ToArray();

        var isSatisfied = higherOrderPredicate(underlyingResults);

        var causes = new Lazy<BooleanResult<TModel, string>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());

        var assertion = new Lazy<string>(() =>
            {
                var booleanCollectionResults = new HigherOrderBooleanResultEvaluation<TModel, string>(
                    underlyingResults,
                    causes.Value);

                return isSatisfied
                    ? trueBecause(booleanCollectionResults)
                    : falseBecause(booleanCollectionResults);
            });

        var lazyDescription = new Lazy<ResultDescriptionBase>(() =>
            new HigherOrderExpressionTreeResultDescription<string>(
                isSatisfied,
                assertion.Value,
                [],
                expression,
                causes.Value,
                Description.Statement));

        return new HigherOrderPolicyResult<string, string>(
            isSatisfied,
            () => assertion.Value,
            () => assertion.Value.ToEnumerable(),
            () => assertion.Value.ToEnumerable(),
            () => lazyDescription.Value,
            underlyingResults,
            () => causes.Value);
    }
}
