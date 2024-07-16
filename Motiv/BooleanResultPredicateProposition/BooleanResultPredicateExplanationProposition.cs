namespace Motiv.BooleanResultPredicateProposition;

internal sealed class BooleanResultPredicateExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription specDescription)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(TModel model)
    {
        var predicateResult = predicate(model);
        var assertion = GetLazyAssertion(model, predicateResult);

        return CreatePolicyResult(assertion, predicateResult);
    }

    private Lazy<string> GetLazyAssertion(TModel model, BooleanResultBase<TUnderlyingMetadata> booleanResult)
    {
        return new Lazy<string>(() =>
            booleanResult.Satisfied switch
            {
                true => whenTrue(model, booleanResult),
                false => whenFalse(model, booleanResult)
            });
    }

    private PolicyResultBase<string> CreatePolicyResult(Lazy<string> assertion, BooleanResultBase<TUnderlyingMetadata> booleanResult)
    {
        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertion.Value, booleanResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(
                assertion.Value.ToEnumerable(),
                booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<string>> ?? []));

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                assertion.Value,
                Description.Statement));

        return new PolicyResultWithUnderlying<string, TUnderlyingMetadata>(
            booleanResult,
            Value,
            MetadataTier,
            Explanation,
            ResultDescription);

        string Value() => assertion.Value;
        MetadataNode<string> MetadataTier() => metadataTier.Value;
        Explanation Explanation() => explanation.Value;
        ResultDescriptionBase ResultDescription() => resultDescription.Value;
    }
}
