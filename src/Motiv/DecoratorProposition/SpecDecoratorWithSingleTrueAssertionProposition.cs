using System.Threading;
using Motiv.Shared;

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
        var underlyingResult = underlyingSpec.Evaluate(model);
        BooleanResultBase<TUnderlyingMetadata>[] underlyingResults = [underlyingResult];

        var because = new Lazy<string>(() =>
            underlyingResult.Satisfied switch
            {
                true => trueBecause,
                false => whenFalse(model, underlyingResult)
            }, LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            because.Value.ElseFallback(() => Description.ToReason(underlyingResult.Satisfied)), LazyThreadSafetyMode.None);

        return new PolicyResultWithUnderlying<string, TUnderlyingMetadata>(
            underlyingResult,
            () => because.Value,
            () => new MetadataNode<string>(because.Value,
                underlyingResults as IEnumerable<BooleanResultBase<string>> ?? []),
            () => new Explanation(
                assertion.Value,
                underlyingResults,
                underlyingResults),
            () => new BooleanResultDescriptionWithUnderlying(
                underlyingResult,
                assertion.Value,
                Description.Statement));
    }
}
