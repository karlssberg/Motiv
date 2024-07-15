namespace Motiv.BooleanResultPredicateProposition;

internal sealed class PolicyResultPredicateExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> predicate,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> whenTrue,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription specDescription)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override PolicyResultBase<string> IsSatisfiedBy(TModel model)
    {
        var predicateResult = predicate(model);
        var assertion = GetLazyAssertion(model, predicateResult);

        return CreatePolicyResult(assertion, predicateResult);
    }

    private Lazy<string> GetLazyAssertion(TModel model, PolicyResultBase<TUnderlyingMetadata> policyResult)
    {
        return new Lazy<string>(() =>
            policyResult.Satisfied switch
            {
                true => whenTrue(model, policyResult),
                false => whenFalse(model, policyResult)
            });
    }

    private static PolicyResultBase<string> CreatePolicyResult(Lazy<string> assertion, PolicyResultBase<TUnderlyingMetadata> policyResult)
    {
        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertion.Value, policyResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(
                assertion.Value.ToEnumerable(),
                policyResult.ToEnumerable() as IEnumerable<PolicyResultBase<string>> ?? []));

        return new PolicyResultWithUnderlying<string, TUnderlyingMetadata>(
            policyResult,
            Value,
            MetadataTier,
            Explanation,
            Reason);

        string Value() => assertion.Value;
        MetadataNode<string> MetadataTier() => metadataTier.Value;
        Explanation Explanation() => explanation.Value;
        string Reason() => assertion.Value;
    }
}
