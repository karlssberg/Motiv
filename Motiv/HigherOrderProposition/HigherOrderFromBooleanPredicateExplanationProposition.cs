namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanPredicateExplanationProposition<TModel>(
    Func<TModel,bool> predicate,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanEvaluation<TModel>, string> trueBecause,
    Func<HigherOrderBooleanEvaluation<TModel>, string> falseBecause,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
    : PolicyBase<IEnumerable<TModel>, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = EvaluateModels(models);
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var assertion = GetLazyAssertion(isSatisfied, underlyingResults);

        return CreatePolicyResult(assertion, isSatisfied);
    }

    private ModelResult<TModel>[] EvaluateModels(IEnumerable<TModel> models) =>
        models
            .Select(model => new ModelResult<TModel>(model, predicate(model)))
            .ToArray();

    private Lazy<string> GetLazyAssertion(bool isSatisfied, ModelResult<TModel>[] underlyingResults) =>
        new(() =>
        {
            var causes = causeSelector(isSatisfied, underlyingResults).ToArray();

            var evaluation = new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causes);

            return isSatisfied
                ? trueBecause(evaluation)
                : falseBecause(evaluation);
        });

    private PolicyResultBase<string> CreatePolicyResult(Lazy<string> assertion, bool isSatisfied)
    {
        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(assertion.Value, []));

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertion.Value, []));

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescription(assertion.Value, Description.Statement));

        return new HigherOrderFromBooleanPredicatePolicyResult<string>(
            isSatisfied,
            Value,
            Metadata,
            Explanation,
            ResultDescription);

        string Value() => assertion.Value;
        MetadataNode<string> Metadata() => metadataTier.Value;
        Explanation Explanation() => explanation.Value;
        ResultDescriptionBase ResultDescription() => resultDescription.Value;
    }
}
