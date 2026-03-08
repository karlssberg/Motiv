using System.Threading;
using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition;

internal sealed class PolicyResultPredicateWithSingleAssertionProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> predicate,
    string trueBecause,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription specDescription)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(TModel model) => predicate(model).Satisfied;

    protected override PolicyResultBase<string> EvaluatePolicy(TModel model)
    {
        var predicateResult = predicate(model);
        PolicyResultBase<TUnderlyingMetadata>[] predicateResults = [predicateResult];

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
                predicateResults as IEnumerable<PolicyResultBase<string>> ?? []),
            () => new Explanation(
                assertion.Value,
                predicateResults,
                predicateResults),
            () => new BooleanResultDescriptionWithUnderlying(
                predicateResult,
                assertion.Value,
                Description.Statement));
    }
}
