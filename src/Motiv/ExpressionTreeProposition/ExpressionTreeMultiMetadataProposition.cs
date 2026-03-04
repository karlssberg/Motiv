using System.Linq.Expressions;
using Motiv.BooleanPredicateProposition;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ExpressionTreeMultiMetadataProposition<TModel, TMetadata, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription description)
    : SpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying { get; } = [];

    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override ISpecDescription Description => description;

    public override bool Matches(TModel model) => _predicate.Match(model);

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model)
    {
        var result = _predicate.Execute(model);

        var metadataResolver =
            result.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        IEnumerable<TMetadata>? metadataResults = null;

        return new PropositionBooleanResult<TMetadata>(
            result.Satisfied,
            () =>
            {
                metadataResults ??= metadataResolver(model, result);
                return new MetadataNode<TMetadata>(metadataResults,
                    result.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? []);
            },
            () =>
            {
                metadataResults ??= metadataResolver(model, result);
                var assertions = metadataResults switch
                {
                    IEnumerable<string> because => because,
                    _ => result.Assertions
                };
                return new Explanation(
                    assertions,
                    result);
            },
            () => new ExpressionTreeBooleanResultDescription(
                result,
                description.ToReason(result.Satisfied),
                expression,
                Description.Statement));
    }
}
