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

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
        {
            metadataResults ??= metadataResolver(model, result);
            return new MetadataNode<TMetadata>(metadataResolver(model, result),
                result.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? []);
        });

        var explanation = new Lazy<Explanation>(() =>
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
        });

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new ExpressionTreeBooleanResultDescription(
                result,
                description.ToReason(result.Satisfied),
                expression,
                Description.Statement));

        return new PropositionBooleanResult<TMetadata>(
            result.Satisfied,
            metadataTier,
            explanation,
            resultDescription);
    }
}
