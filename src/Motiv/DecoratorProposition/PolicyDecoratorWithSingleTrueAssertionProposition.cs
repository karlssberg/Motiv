using System.Threading;
using Motiv.Shared;

namespace Motiv.DecoratorProposition;

internal sealed class PolicyDecoratorWithSingleTrueAssertionProposition<TModel, TUnderlyingMetadata>(
    PolicyBase<TModel, TUnderlyingMetadata> underlyingPolicy,
    string trueBecause,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> whenFalse,
    string? propositionalStatement = null)
    : PolicyBase<TModel, string>
{
    private readonly SpecBase[] _underlying = [underlyingPolicy];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description =>
        new SpecDescription(
            propositionalStatement ?? trueBecause,
            underlyingPolicy.Description);

    public override bool Matches(TModel model) => underlyingPolicy.Matches(model);

    protected override PolicyResultBase<string> EvaluatePolicy(TModel model)
    {
        var underlyingResult = underlyingPolicy.Evaluate(model);
        PolicyResultBase<TUnderlyingMetadata>[] underlyingResults = [underlyingResult];

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
                underlyingResults as IEnumerable<PolicyResultBase<string>> ?? []),
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
