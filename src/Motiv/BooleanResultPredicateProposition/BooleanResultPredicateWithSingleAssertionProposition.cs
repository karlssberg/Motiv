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

    protected override PolicyResultBase<string> EvaluatePolicy(TModel model)
    {
        var predicateResult = predicate(model);

        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> becauseResolver =
            predicateResult.Satisfied
                ? (_, _) => trueBecause
                : whenFalse;

        return new BooleanResultPredicateWithSingleAssertionPolicyResult<TModel, TUnderlyingMetadata>(
            model,
            predicateResult,
            becauseResolver,
            specDescription);
    }
}
