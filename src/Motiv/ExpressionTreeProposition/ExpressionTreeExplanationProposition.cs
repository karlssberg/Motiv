using System.Linq.Expressions;
using System.Threading;
using Motiv.BooleanPredicateProposition;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ExpressionTreeExplanationProposition<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<TModel, BooleanResultBase<string>, string> trueBecause,
    Func<TModel, BooleanResultBase<string>, string> falseBecause,
    ISpecDescription description)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying { get; } = [];

    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => _predicate.Match(model);

    protected override PolicyResultBase<string> EvaluatePolicy(TModel model)
    {
        var result = _predicate.Execute(model);
        BooleanResultBase<string>[] resultArray = [result];

        var because = new Lazy<string>(() =>
            result.Satisfied switch
            {
                true => trueBecause(model, result),
                false => falseBecause(model, result)
            }, LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            because.Value.ElseFallback(() => description.ToReason(result.Satisfied)), LazyThreadSafetyMode.None);

        return new PropositionPolicyResult<string>(
            result.Satisfied,
            () => because.Value,
            () => new MetadataNode<string>(because.Value.ToEnumerable(),
                resultArray),
            () => new Explanation(
                assertion.Value,
                resultArray,
                resultArray),
            () => new ExpressionTreeBooleanResultDescription(
                result,
                assertion.Value,
                expression,
                Description.Statement));
    }
}
