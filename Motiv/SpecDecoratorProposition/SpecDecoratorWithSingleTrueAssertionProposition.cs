using Motiv.Shared;

namespace Motiv.SpecDecoratorProposition;

internal sealed class SpecDecoratorWithSingleTrueAssertionProposition<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    string trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    string? propositionalStatement = null)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => underlyingSpec.ToEnumerable();

    public override ISpecDescription Description =>
        new SpecDescription(
            propositionalStatement ?? trueBecause,
            underlyingSpec.Description);

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(TModel model)
    {
        var underlyingResult = underlyingSpec.IsSatisfiedBy(model);

        var assertion = GetLazyAssertion(model, underlyingResult);

        return CreatePolicyResult(underlyingResult, assertion);
    }

    private Lazy<string> GetLazyAssertion(TModel model, BooleanResultBase<TUnderlyingMetadata> booleanResult)
    {
        return new Lazy<string>(() =>
            booleanResult.Satisfied switch
            {
                true => trueBecause,
                false => whenFalse(model, booleanResult)
            });
    }

    private PolicyResultBase<string> CreatePolicyResult(BooleanResultBase<TUnderlyingMetadata> booleanResult, Lazy<string> assertion)
    {
        var reason = propositionalStatement is not null
            ? propositionalStatement.ToReason(booleanResult.Satisfied)
            : assertion.Value;

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertion.Value, booleanResult.ToEnumerable(), booleanResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(assertion.Value,
                booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<string>> ?? []));

        var description = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                reason,
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
        ResultDescriptionBase ResultDescription() => description.Value;
    }
}
