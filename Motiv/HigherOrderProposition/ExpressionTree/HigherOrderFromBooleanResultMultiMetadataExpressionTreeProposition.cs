using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.HigherOrderProposition.ExpressionTree;

internal sealed class HigherOrderFromBooleanResultMultiMetadataExpressionTreeProposition<TModel, TMetadata, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

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
                _ => [expression.ToAssertion(isSatisfied)]
            });

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
                new HigherOrderResultDescription<string>(
                    Description.ToAssertion(isSatisfied),
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
