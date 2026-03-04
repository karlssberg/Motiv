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
    public override IEnumerable<SpecBase> Underlying => underlyingSpec.ToEnumerable();

    public override ISpecDescription Description =>
        new SpecDescription(
            propositionalStatement ?? trueBecause,
            underlyingSpec.Description);

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(TModel model)
    {
        var underlyingResult = underlyingSpec.IsSatisfiedBy(model);

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
                underlyingResult.ToEnumerable() as IEnumerable<BooleanResultBase<string>> ?? []), LazyThreadSafetyMode.None);

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
