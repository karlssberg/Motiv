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

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<TMetadata> EvaluateSpec(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        return new HigherOrderFromExpressionTreeMultiMetadataBooleanResult<TModel, TMetadata>(
            isSatisfied,
            underlyingResults,
            whenTrue,
            whenFalse,
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
