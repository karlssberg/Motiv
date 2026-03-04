using System.Threading;
using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition;

internal sealed class BooleanResultPredicateWithSingleAssertionProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate,
    string trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription specDescription)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => [];


    public override ISpecDescription Description => specDescription;

    public override bool Matches(TModel model) => predicate(model).Satisfied;

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(TModel model)
    {
        var predicateResult = predicate(model);

        var assertion = new Lazy<string>(() =>
            predicateResult.Satisfied switch
            {
                true => trueBecause,
                false => whenFalse(model, predicateResult)
            }, LazyThreadSafetyMode.None);

        return new PolicyResultWithUnderlying<string, TUnderlyingMetadata>(
            predicateResult,
            () => assertion.Value,
            () => new MetadataNode<string>(
                assertion.Value.ToEnumerable(),
                predicateResult.ToEnumerable() as IEnumerable<BooleanResultBase<string>> ?? []),
            () => new Explanation(
                assertion.Value,
                predicateResult.ToEnumerable(),
                predicateResult.ToEnumerable()),
            () => new BooleanResultDescriptionWithUnderlying(
                predicateResult,
                assertion.Value,
                Description.Statement));
    }
}
