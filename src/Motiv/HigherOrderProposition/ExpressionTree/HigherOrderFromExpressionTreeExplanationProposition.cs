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
        IEnumerable<BooleanResult<TModel, string>>> causeSelector,
    HigherOrderShortCircuit? shortCircuit = null)
    : PolicyBase<IEnumerable<TModel>, string>
{
    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => description;

    public override bool Matches(IEnumerable<TModel> models) =>
        shortCircuit is { } sc
            ? sc.Evaluate(models, _predicate, static (m, p) => p.Execute(m).Satisfied)
            : EvaluateModels(models).IsSatisfied;

    protected override PolicyResultBase<string> EvaluatePolicy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        return new HigherOrderFromExpressionTreeExplanationPolicyResult<TModel>(
            isSatisfied,
            underlyingResults,
            trueBecause,
            falseBecause,
            description,
            expression,
            causeSelector);
    }

    private (BooleanResult<TModel, string>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = HigherOrderResults.Materialize(models, _predicate,
            static (model, predicate) => new BooleanResult<TModel, string>(model, predicate.Execute(model)));
        return (results, higherOrderPredicate(results));
    }
}
