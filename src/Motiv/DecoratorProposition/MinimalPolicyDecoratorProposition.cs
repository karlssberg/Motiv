namespace Motiv.DecoratorProposition;

internal sealed class MinimalPolicyDecoratorProposition<TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> underlyingPolicy,
    ISpecDescription description)
    : PolicyBase<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [underlyingPolicy];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingPolicy.Matches(model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model)
    {
        var predicateResult = underlyingPolicy.Evaluate(model);

        return new MinimalPolicyDecoratorPolicyResult<TMetadata>(predicateResult, description);
    }
}
