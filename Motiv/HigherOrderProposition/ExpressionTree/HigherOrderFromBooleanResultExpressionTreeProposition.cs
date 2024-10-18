using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.HigherOrderProposition.ExpressionTree;

internal sealed class HigherOrderFromBooleanResultExpressionTreeProposition<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, string>
{
    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    protected override BooleanResultBase<string> IsSpecSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new BooleanResult<TModel, string>(model,  _predicate.Execute(model)))
            .ToArray();

        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = new Lazy<BooleanResult<TModel, string>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());

        var metadata = new Lazy<IEnumerable<string>>(() =>
            causes.Value.SelectMany(result => result.MetadataTier.Metadata).DistinctWithOrderPreserved());

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
                new HigherOrderResultDescription<string>(
                    Description.ToAssertion(isSatisfied),
                    [],
                    causes.Value,
                    Description.Statement));

        return new HigherOrderBooleanResult<string, string>(
            isSatisfied,
            Metadata,
            Assertions,
            ResultDescription,
            underlyingResults,
            GetCauses);

        IEnumerable<string> Metadata() => metadata.Value;
        IEnumerable<string> Assertions() => metadata.Value;
        IEnumerable<BooleanResult<TModel, string>> GetCauses() => causes.Value;
        ResultDescriptionBase ResultDescription() => resultDescription.Value;
    }
}
