using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.HigherOrderProposition.ExpressionTree;

internal sealed class HigherOrderFromBooleanResultMetadataExpressionTreeProposition<TModel, TMetadata, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> whenTrue,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
    : PolicyBase<IEnumerable<TModel>, TMetadata>
{
    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new BooleanResult<TModel, string>(model,  _predicate.Execute(model)))
            .ToArray();
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = new Lazy<BooleanResult<TModel, string>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());

        var metadata = new Lazy<TMetadata>(() =>
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

        var lazyDescription = new Lazy<ResultDescriptionBase>(() =>
            new HigherOrderResultDescription<string>(
                Description.ToAssertion(isSatisfied),
                [],
                causes.Value,
                Description.Statement));

        return new HigherOrderPolicyResult<TMetadata, string>(
            isSatisfied,
            Value,
            Metadata,
            Assertions,
            ResultDescription,
            underlyingResults,
            GetCauses);

        TMetadata Value() => metadata.Value;
        IEnumerable<TMetadata> Metadata() => metadata.Value.ToEnumerable();
        IEnumerable<string> Assertions() => assertions.Value;
        ResultDescriptionBase ResultDescription() => lazyDescription.Value;
        IEnumerable<BooleanResult<TModel, string>> GetCauses() => causes.Value;
    }
}
