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

    protected override PolicyResultBase<string> IsPolicySatisfiedBy(TModel model)
    {
        var predicateResult = predicate(model);

        var assertion = new Lazy<string>(() =>
            predicateResult.Satisfied switch
            {
                true => trueBecause,
                false => whenFalse(model, predicateResult)
            });

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertion.Value, predicateResult.ToEnumerable(), predicateResult.ToEnumerable()));

        var metadataTier = new Lazy<MetadataNode<string>>(() =>
            new MetadataNode<string>(
                assertion.Value.ToEnumerable(),
                predicateResult.ToEnumerable() as IEnumerable<BooleanResultBase<string>> ?? []));

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                predicateResult,
                assertion.Value,
                Description.Statement));

        return new PolicyResultWithUnderlying<string, TUnderlyingMetadata>(
            predicateResult,
            () => assertion.Value,
            () => metadataTier.Value,
            () => explanation.Value,
            () => resultDescription.Value);
    }
}
