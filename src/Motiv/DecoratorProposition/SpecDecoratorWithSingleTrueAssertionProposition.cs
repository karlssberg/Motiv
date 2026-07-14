namespace Motiv.DecoratorProposition;

internal sealed class SpecDecoratorWithSingleTrueAssertionProposition<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    string trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription description)
    : PolicyBase<TModel, string>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override PolicyResultBase<string> EvaluatePolicy(TModel model)
    {
        var underlyingResult = underlyingSpec.EvaluateInternal(model);

        return new SpecDecoratorWithSingleTrueAssertionPolicyResult<TModel, TUnderlyingMetadata>(
            underlyingResult,
            model,
            trueBecause,
            whenFalse,
            description);
    }
}
