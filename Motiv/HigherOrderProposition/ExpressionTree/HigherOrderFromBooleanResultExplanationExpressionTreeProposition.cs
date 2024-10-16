using System.Linq.Expressions;

namespace Motiv.HigherOrderProposition.ExpressionTree;

internal sealed class HigherOrderFromBooleanResultExplanationExpressionTreeProposition<TModel>(
    Expression<Func<TModel, bool>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> trueBecause,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> falseBecause,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>,
        IEnumerable<BooleanResult<TModel, string>>> causeSelector)
    : PolicyBase<IEnumerable<TModel>, string>
{
    private readonly SpecBase<TModel, string> _spec = expression.ToSpec();

    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = EvaluateModels(models);

        var isSatisfied = higherOrderPredicate(underlyingResults);

        var causes = DetermineCauses(isSatisfied, underlyingResults);

        var assertion = GetLazyAssertion(underlyingResults, causes, isSatisfied);

        return CreatePolicyResult(isSatisfied, underlyingResults, assertion, causes);
    }

    private BooleanResult<TModel, string>[] EvaluateModels(IEnumerable<TModel> models)
    {
        return models
            .Select(model => new BooleanResult<TModel, string>(model, _spec.IsSatisfiedBy(model)))
            .ToArray();
    }

    private Lazy<BooleanResult<TModel, string>[]> DetermineCauses(bool isSatisfied,
        BooleanResult<TModel, string>[] underlyingResults)
    {
        return new Lazy<BooleanResult<TModel, string>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());
    }

    private Lazy<string> GetLazyAssertion(BooleanResult<TModel, string>[] underlyingResults,
        Lazy<BooleanResult<TModel, string>[]> causes, bool isSatisfied)
    {
        return new Lazy<string>(() =>
        {
            var booleanCollectionResults = new HigherOrderBooleanResultEvaluation<TModel, string>(
                underlyingResults,
                causes.Value);

            return isSatisfied
                ? trueBecause(booleanCollectionResults)
                : falseBecause(booleanCollectionResults);
        });
    }

    private PolicyResultBase<string> CreatePolicyResult(bool isSatisfied,
        BooleanResult<TModel, string>[] underlyingResults,
        Lazy<string> assertion, Lazy<BooleanResult<TModel, string>[]> causes)
    {
        var lazyDescription = new Lazy<ResultDescriptionBase>(() =>
            new HigherOrderResultDescription<string>(
                assertion.Value,
                [],
                causes.Value,
                Description.Statement));

        return new HigherOrderPolicyResult<string, string>(
            isSatisfied,
            Value,
            Metadata,
            Assertions,
            ResultDescription,
            underlyingResults,
            GetCauses);

        string Value() => assertion.Value;
        IEnumerable<string> Metadata() => assertion.Value.ToEnumerable();
        IEnumerable<string> Assertions() => assertion.Value.ToEnumerable();
        ResultDescriptionBase ResultDescription() => lazyDescription.Value;
        IEnumerable<BooleanResult<TModel, string>> GetCauses() => causes.Value;
    }
}
