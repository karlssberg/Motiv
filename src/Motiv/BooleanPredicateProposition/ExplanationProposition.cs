using System.Threading;
using Motiv.Shared;

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

        var because = new Lazy<string>(() => becauseResolver(model), LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            because.Value.ElseFallback(() => Description.ToReason(isSatisfied)), LazyThreadSafetyMode.None);

        return new PropositionPolicyResult<string>(
            isSatisfied,
            () => because.Value,
            () => new MetadataNode<string>(because.Value),
            () => new Explanation(assertion.Value),
            () => new PropositionResultDescription(assertion.Value, Description.Statement));
    }
}
