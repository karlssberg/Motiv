namespace Motiv.SpecDecoratorProposition;

internal sealed class SpecDecoratorExplanationProposition<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause,
    ISpecDescription description)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => UnderlyingSpec.ToEnumerable();


    public override ISpecDescription Description => description;

    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; } = underlyingSpec;

    public override PolicyResultBase<string> Execute(TModel model)
    {
        var predicateResult = UnderlyingSpec.IsSatisfiedBy(model);
        var assertion = GetLazyAssertion(model, predicateResult);

        return CreatePolicyResult(assertion, predicateResult);
    }

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) =>
        Execute(model);

    private Lazy<string> GetLazyAssertion(TModel model, BooleanResultBase<TUnderlyingMetadata> predicateResult)
    {
        return new Lazy<string>(() =>
            predicateResult.Satisfied switch
            {
                true => trueBecause(model, predicateResult),
                false => falseBecause(model, predicateResult)
            });
    }

    private static PolicyResultBase<string> CreatePolicyResult(Lazy<string> assertion, BooleanResultBase<TUnderlyingMetadata> booleanResult)
    {
        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertion.Value, booleanResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(assertion.Value.ToEnumerable(),
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
        string Reason() => assertion.Value;
    }
}
