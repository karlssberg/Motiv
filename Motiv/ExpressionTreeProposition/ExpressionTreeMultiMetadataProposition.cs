using System.Linq.Expressions;
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

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model)
    {
        var result = _predicate.Execute(model);

        var metadata = new Lazy<IEnumerable<TMetadata>>(() =>
            result.Satisfied switch
            {
                true => whenTrue(model, result),
                false => whenFalse(model, result)
            });

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(metadata.Value,
                result.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? []));

        var explanation = new Lazy<Explanation>(() =>
        {
            var assertions = metadata.Value switch
            {
                IEnumerable<string> because => because.ToArray(),
                _ => [Description.ToAssertion(result.Satisfied)]
            };
            return new Explanation(assertions, result.ToEnumerable(), result.ToEnumerable());
        });

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                result,
                Description.ToAssertion(result.Satisfied),
                Description.Statement));

        return new ExpressionTreeBooleanResult<TMetadata>(
            result.Satisfied,
            metadataTier,
            explanation,
            resultDescription);
    }
}
