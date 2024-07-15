namespace Motiv.BooleanResultPredicateProposition;

internal sealed class PolicyResultPredicateWithSingleAssertionProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> predicate,
    string trueBecause,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> whenFalse,
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

    private Lazy<string> GetLazyAssertion(TModel model, PolicyResultBase<TUnderlyingMetadata> policyResult) =>
        new(() =>
            policyResult.Satisfied switch
            {
                true => trueBecause,
                false => whenFalse(model, policyResult)
            });

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
