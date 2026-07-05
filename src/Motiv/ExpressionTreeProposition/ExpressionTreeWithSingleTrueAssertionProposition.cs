using System.Linq.Expressions;
using System.Threading;
using Motiv.BooleanPredicateProposition;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

/// <summary>
/// An proposition representing a lambda expression tree.
/// </summary>
internal sealed class ExpressionTreeWithSingleTrueAssertionProposition<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    string trueBecause,
    Func<TModel, BooleanResultBase<string>, string> whenFalse,
    ISpecDescription description)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => _predicate.Match(model);

    protected override PolicyResultBase<string> EvaluatePolicy(TModel model)
    {
        var result = _predicate.Execute(model);
        BooleanResultBase<string>[] resultAsCollection = [result];

        var because = new Lazy<string>(() =>
            result.Satisfied switch
            {
                true => trueBecause,
                false => whenFalse(model, result)
            }, LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            because.Value.ElseFallback(() => description.ToReason(result.Satisfied)), LazyThreadSafetyMode.None);

        return new PropositionPolicyResult<string>(
            result.Satisfied,
            () => because.Value,
            () => new MetadataNode<string>(because.Value,
                resultAsCollection),
            () => new Explanation(
                assertion.Value,
                resultAsCollection,
                resultAsCollection),
            () => new ExpressionTreeBooleanResultDescription(
                result,
                assertion.Value,
                expression,
                Description.Statement));
    }
}
