using System.Linq.Expressions;
using System.Threading;
using Motiv.ExpressionTreeProposition;

namespace Motiv.HigherOrderProposition.ExpressionTree;

internal sealed class MinimalHigherOrderFromExpressionTreeProposition<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    ISpecDescription description,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, string>
{
    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => description;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<string> IsSpecSatisfiedBy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);
        var causes = new Lazy<BooleanResult<TModel, string>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray(), LazyThreadSafetyMode.None);

        var metadata = new Lazy<IEnumerable<string>>(() =>
            causes.Value.SelectMany(result => result.MetadataTier.Metadata), LazyThreadSafetyMode.None);

        return new HigherOrderBooleanResult<string, string>(
            isSatisfied,
            () => metadata.Value,
            () => metadata.Value,
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
