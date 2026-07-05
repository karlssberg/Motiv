namespace Motiv.BooleanPredicateProposition;

/// <summary>
/// An explanation proposition whose statement is derived from the WhenTrue assertion. The because-strings
/// double as the explanation; degenerate (null/empty/whitespace) strings fall back to the statement-derived reason.
/// </summary>
internal sealed class ExplanationProposition<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    ISpecDescription specDescription)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(TModel model) => predicate(model);

    protected override PolicyResultBase<string> EvaluatePolicy(TModel model)
    {
        var isSatisfied = predicate(model);

        var becauseResolver =
            isSatisfied switch
            {
                true => trueBecause,
                false => falseBecause
            };

        return new ExplanationPropositionPolicyResult<TModel>(
            isSatisfied,
            model,
            becauseResolver,
            specDescription);
    }
}
