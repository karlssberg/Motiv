using System.Linq.Expressions;
using Motiv.BooleanPredicateProposition;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ExpressionTreeMultiAssertionExplanationProposition<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<TModel, BooleanResultBase<string>, IEnumerable<string>> whenTrue,
    Func<TModel, BooleanResultBase<string>, IEnumerable<string>> whenFalse,
    ISpecDescription description)
    : SpecBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying { get; } = [];

    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => _predicate.Match(model);

    protected override BooleanResultBase<string> EvaluateSpec(TModel model)
    {
        var result = _predicate.Execute(model);
        BooleanResultBase<string>[] resultArray = [result];

        var metadataResolver =
            result.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        IEnumerable<string>? metadataResults = null;

        return new PropositionBooleanResult<string>(
            result.Satisfied,
            () =>
            {
                metadataResults ??= metadataResolver(model, result);
                return new MetadataNode<string>(metadataResults,
                    resultArray as IEnumerable<BooleanResultBase<string>> ?? []);
            },
            () =>
            {
                metadataResults ??= metadataResolver(model, result);
                return new Explanation(
                    metadataResults.ElseFallback(() => result.Assertions),
                    result);
            },
            () => new ExpressionTreeBooleanResultDescription(
                result,
                description.ToReason(result.Satisfied),
                expression,
                Description.Statement));
    }
}
