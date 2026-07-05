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

        Func<TModel, PolicyResultBase<TUnderlyingMetadata>, string> becauseResolver =
            predicateResult.Satisfied
                ? (_, _) => trueBecause
                : whenFalse;

        return new PolicyResultPredicateWithSingleAssertionPolicyResult<TModel, TUnderlyingMetadata>(
            model,
            predicateResult,
            becauseResolver,
            specDescription);
    }
}
