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

        var assertion = new Lazy<string>(() =>
            result.Satisfied switch
            {
                true => trueBecause,
                false => whenFalse(model, result)
            }, LazyThreadSafetyMode.None);

        var reason = new Lazy<string>(() =>
            description.HasExplicitStatement
            ? description.ToReason(result.Satisfied)
            : assertion.Value, LazyThreadSafetyMode.None);

        return new PropositionPolicyResult<string>(
            result.Satisfied,
            () => assertion.Value,
            () => new MetadataNode<string>(assertion.Value,
                resultAsCollection),
            () => new Explanation(
                assertion.Value,
                resultAsCollection,
                resultAsCollection),
            () => new ExpressionTreeBooleanResultDescription(
                result,
                reason.Value,
                expression,
                Description.Statement));
    }
}
