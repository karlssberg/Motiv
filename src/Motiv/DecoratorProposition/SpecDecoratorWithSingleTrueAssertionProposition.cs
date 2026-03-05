using System.Threading;
using Motiv.Shared;

namespace Motiv.DecoratorProposition;

internal sealed class SpecDecoratorWithSingleTrueAssertionProposition<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    string trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    string? propositionalStatement = null)
    : PolicyBase<TModel, string>
{
    private readonly SpecBase[] _underlying = [underlyingSpec];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description =>
        new SpecDescription(
            propositionalStatement ?? trueBecause,
            underlyingSpec.Description);

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(TModel model)
    {
        var underlyingResult = underlyingSpec.IsSatisfiedBy(model);
        BooleanResultBase<TUnderlyingMetadata>[] underlyingResults = [underlyingResult];

        var assertion = new Lazy<string>(() =>
            underlyingResult.Satisfied switch
            {
                true => trueBecause,
                false => whenFalse(model, underlyingResult)
            }, LazyThreadSafetyMode.None);

        return new PolicyResultWithUnderlying<string, TUnderlyingMetadata>(
            underlyingResult,
            () => assertion.Value,
            () => new MetadataNode<string>(assertion.Value,
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
