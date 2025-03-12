using Motiv.Shared;

namespace Motiv.SpecDecoratorProposition;

internal sealed class MinimalSpecDecoratorProposition<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> underlyingSpec,
    ISpecDescription description)
    : SpecBase<TModel, TMetadata>
{
    private readonly string _trueBecause = GetTrueAssertion(underlyingSpec.Statement);
    private readonly string _falseBecause = GetFalseAssertion(underlyingSpec.Statement);
    public override IEnumerable<SpecBase> Underlying => UnderlyingSpec.ToEnumerable();

    public override ISpecDescription Description => description;

    public SpecBase<TModel, TMetadata> UnderlyingSpec { get; } = underlyingSpec;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model)
    {
        var predicateResult = UnderlyingSpec.IsSatisfiedBy(model);
        var assertion = GetLazyAssertion(model, predicateResult);

        return CreateSpecResult(assertion, predicateResult);
    }

    private Lazy<string> GetLazyAssertion(TModel model, BooleanResultBase<TMetadata> predicateResult)
    {
        return new Lazy<string>(() =>
            predicateResult.Satisfied switch
            {
                true => _trueBecause,
                false => _falseBecause
            });
    }

    private BooleanResultBase<TMetadata> CreateSpecResult(Lazy<string> assertion, BooleanResultBase<TMetadata> booleanResult)
    {
        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                assertion.Value,
                Description.Statement));

        return new BooleanResultWithUnderlying<TMetadata, TMetadata>(
            booleanResult,
            Value,
            MetadataTier,
            ResultDescription);

        MetadataNode<TMetadata> Value() => booleanResult.MetadataTier;
        Explanation MetadataTier() => booleanResult.Explanation;
        ResultDescriptionBase ResultDescription() => resultDescription.Value;
    }


    private static string GetTrueAssertion(string proposition) =>
        proposition.ContainsReservedCharacters()
            ? $"({proposition})"
            : proposition;

    private static string GetFalseAssertion(string proposition) =>
        proposition.ContainsReservedCharacters()
            ? $"({proposition})".AsUnsatisfied()
            : proposition.AsUnsatisfied();
}
