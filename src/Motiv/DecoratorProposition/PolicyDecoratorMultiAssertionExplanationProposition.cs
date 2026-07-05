namespace Motiv.DecoratorProposition;

/// <summary>
/// Represents a proposition that yields a collection of assertions based on the result of an underlying policy. The
/// because-strings double as the assertions; degenerate (null/empty/whitespace) strings fall back to the
/// statement-derived reason.
/// </summary>
internal sealed class PolicyDecoratorMultiAssertionExplanationProposition<TModel, TUnderlyingMetadata>(
    PolicyBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, IEnumerable<string>> whenTrue,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, IEnumerable<string>> whenFalse,
    ISpecDescription description)
    : SpecBase<TModel, string>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override BooleanResultBase<string> EvaluateSpec(TModel model)
    {
        var policyResult = underlyingSpec.Evaluate(model);

        var assertionsResolver =
            policyResult.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        return new PolicyDecoratorMultiAssertionExplanationBooleanResult<TModel, TUnderlyingMetadata>(
            policyResult,
            model,
            assertionsResolver,
            description);
    }
}
