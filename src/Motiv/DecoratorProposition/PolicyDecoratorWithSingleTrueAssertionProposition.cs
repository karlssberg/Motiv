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
    public override IEnumerable<SpecBase> Underlying => underlyingPolicy.ToEnumerable();

    public override ISpecDescription Description =>
        new SpecDescription(
            propositionalStatement ?? trueBecause,
            underlyingPolicy.Description);

    public override bool Matches(TModel model) => underlyingPolicy.Matches(model);

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(TModel model)
    {
        var underlyingResult = underlyingPolicy.IsSatisfiedBy(model);

        var assertion = new Lazy<string>(() =>
            underlyingResult.Satisfied switch
            {
                true => trueBecause,
                false => whenFalse(model, underlyingResult)
            }, LazyThreadSafetyMode.None);

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(
                assertion.Value,
                underlyingResult.ToEnumerable(),
                underlyingResult.ToEnumerable()), LazyThreadSafetyMode.None);

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(assertion.Value,
                underlyingResult.ToEnumerable() as IEnumerable<PolicyResultBase<string>> ?? []), LazyThreadSafetyMode.None);

        var description = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                underlyingResult,
                assertion.Value,
                Description.Statement), LazyThreadSafetyMode.None);

        return new PolicyResultWithUnderlying<string, TUnderlyingMetadata>(
            underlyingResult,
            assertion,
            metadataTier,
            explanation,
            description);
    }
}
