namespace Motiv.DecoratorProposition;

internal sealed class MinimalSpecDecoratorProposition<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> underlyingSpec,
    ISpecDescription description)
    : SpecBase<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var predicateResult = underlyingSpec.EvaluateInternal(model);

        return new MinimalSpecDecoratorBooleanResult<TMetadata>(predicateResult, description);
    }
}
