namespace Motiv.DecoratorProposition;

internal sealed class PolicyDecoratorWithSingleTrueAssertionProposition<TModel, TUnderlyingMetadata>(
    PolicyBase<TModel, TUnderlyingMetadata> underlyingPolicy,
    string trueBecause,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription description)
    : PolicyBase<TModel, string>
{
    private readonly SpecBase[] _underlying = [underlyingPolicy];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingPolicy.Matches(model);

    protected override PolicyResultBase<string> EvaluatePolicy(TModel model)
    {
        var underlyingResult = underlyingPolicy.Evaluate(model);

        return new PolicyDecoratorWithSingleTrueAssertionPolicyResult<TModel, TUnderlyingMetadata>(
            underlyingResult,
            model,
            trueBecause,
            whenFalse,
            description);
    }
}
