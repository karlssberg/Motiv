using System.Threading;
using Motiv.Shared;

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
        var booleanResult = underlyingSpec.Evaluate(model);
        BooleanResultBase<TUnderlyingMetadata>[] booleanResults = [booleanResult];

        var metadata = new Lazy<IEnumerable<string>>(() =>
            booleanResult.Satisfied switch
            {
                true => whenTrue(model, booleanResult),
                false => whenFalse(model, booleanResult)
            }, LazyThreadSafetyMode.None);

        var assertions = new Lazy<string[]>(() =>
            metadata.Value
                .ElseFallback(() => Description.ToReason(booleanResult.Satisfied))
                .ToArray(), LazyThreadSafetyMode.None);

        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            booleanResult,
            () => new MetadataNode<string>(metadata.Value,
                booleanResults as IEnumerable<BooleanResultBase<string>> ?? []),
            () => new Explanation(
                assertions.Value,
                booleanResults,
                booleanResults),
            () => new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                Description.ToReason(booleanResult.Satisfied),
                Description.Statement));
    }
}
