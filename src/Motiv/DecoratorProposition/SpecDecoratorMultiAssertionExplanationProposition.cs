namespace Motiv.DecoratorProposition;

/// <summary>
/// Represents a proposition that yields a collection of assertions based on the result of an underlying spec. The
/// because-strings double as the assertions; degenerate (null/empty/whitespace) strings fall back to the
/// statement-derived reason.
/// </summary>
internal sealed class SpecDecoratorMultiAssertionExplanationProposition<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> whenFalse,
    ISpecDescription description)
    : SpecBase<TModel, string>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override BooleanResultBase<string> EvaluateSpec(TModel model)
    {
        var booleanResult = underlyingSpec.EvaluateInternal(model);

        var assertionsResolver =
            booleanResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        return new SpecDecoratorMultiAssertionExplanationBooleanResult<TModel, TUnderlyingMetadata>(
            booleanResult,
            model,
            assertionsResolver,
            description);
    }
}
