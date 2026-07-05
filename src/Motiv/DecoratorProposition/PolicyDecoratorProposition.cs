namespace Motiv.DecoratorProposition;

internal sealed class PolicyDecoratorProposition<TModel, TMetadata, TUnderlyingMetadata>(
    PolicyBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription description)
    : PolicyBase<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model)
    {
        var policyResult = underlyingSpec.Evaluate(model);

        var valueResolver =
            policyResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        return new PolicyDecoratorPolicyResult<TModel, TMetadata, TUnderlyingMetadata>(
            policyResult,
            model,
            valueResolver,
            description);
    }
}
