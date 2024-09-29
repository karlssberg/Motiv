using Motiv.Shared;

namespace Motiv.SpecDecoratorProposition;

internal sealed class PolicyDecoratorExplanationProposition<TModel, TUnderlyingMetadata>(
    PolicyBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> trueBecause,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> falseBecause,
    ISpecDescription description)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => UnderlyingPolicy.ToEnumerable();

    public override ISpecDescription Description => description;

    public PolicyBase<TModel, TUnderlyingMetadata> UnderlyingPolicy { get; } = underlyingSpec;

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(TModel model)
    {
        var predicateResult = UnderlyingPolicy.IsSatisfiedBy(model);
        var assertion = GetLazyAssertion(model, predicateResult);

        return CreatePolicyResult(assertion, predicateResult);
    }

    private Lazy<string> GetLazyAssertion(TModel model, PolicyResultBase<TUnderlyingMetadata> predicateResult)
    {
        return new Lazy<string>(() =>
            predicateResult.Satisfied switch
            {
                true => trueBecause(model, predicateResult),
                false => falseBecause(model, predicateResult)
            });
    }

    private PolicyResultBase<string> CreatePolicyResult(Lazy<string> assertion, PolicyResultBase<TUnderlyingMetadata> policyResult)
    {
        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertion.Value, policyResult.ToEnumerable(), policyResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(assertion.Value.ToEnumerable(),
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
