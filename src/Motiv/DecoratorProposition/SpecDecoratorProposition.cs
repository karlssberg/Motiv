namespace Motiv.DecoratorProposition;

internal sealed class SpecDecoratorProposition<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription description)
    : PolicyBase<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model)
    {
        var booleanResult = underlyingSpec.Evaluate(model);

        var metadataResolver =
            booleanResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        return new SpecDecoratorPolicyResult<TModel, TMetadata, TUnderlyingMetadata>(
            booleanResult,
            model,
            metadataResolver,
            description);
    }
}
