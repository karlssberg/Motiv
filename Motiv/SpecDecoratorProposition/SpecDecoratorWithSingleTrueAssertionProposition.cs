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

    public override PolicyResultBase<string> Execute(TModel model)
    {
        var underlyingResult = underlyingSpec.IsSatisfiedBy(model);

        var assertion = GetLazyAssertion(model, underlyingResult);

        return CreatePolicyResult(underlyingResult, assertion);
    }

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) =>
        Execute(model);

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
            new Explanation(assertion.Value, booleanResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(assertion.Value,
                booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<string>> ?? []));

        return new PolicyResultWithUnderlying<string, TUnderlyingMetadata>(
            booleanResult,
            Value,
            MetadataTier,
            Explanation,
            Reason);

        string Value() => assertion.Value;
        MetadataNode<string> MetadataTier() => metadataTier.Value;
        Explanation Explanation() => explanation.Value;
        string Reason() => reason;
    }
}
