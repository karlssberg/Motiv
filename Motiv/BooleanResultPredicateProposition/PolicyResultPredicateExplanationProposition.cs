﻿namespace Motiv.BooleanResultPredicateProposition;

internal sealed class PolicyResultPredicateExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> predicate,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> whenTrue,
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

    private Lazy<string> GetLazyAssertion(TModel model, PolicyResultBase<TUnderlyingMetadata> policyResult)
    {
        return new Lazy<string>(() =>
            policyResult.Satisfied switch
            {
                true => whenTrue(model, policyResult),
                false => whenFalse(model, policyResult)
            });
    }

    private PolicyResultBase<string> CreatePolicyResult(Lazy<string> assertion, PolicyResultBase<TUnderlyingMetadata> policyResult)
    {
        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertion.Value, policyResult.ToEnumerable(), policyResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(
                assertion.Value.ToEnumerable(),
                policyResult.ToEnumerable() as IEnumerable<PolicyResultBase<string>> ?? []));

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                policyResult,
                assertion.Value,
                Description.Statement));

        return new PolicyResultWithUnderlying<string, TUnderlyingMetadata>(
            policyResult,
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
