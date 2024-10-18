using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ExpressionTreeMetadataProposition<TModel, TMetadata, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<TModel, BooleanResultBase<string>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<string>, TMetadata> whenFalse,
    ISpecDescription description)
    : PolicyBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying { get; } = [];

    private readonly ExpressionPredicate<TModel, TPredicateResult> _predicate = new(expression);

    public override ISpecDescription Description => description;

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var result = _predicate.Execute(model);

        var lazyMetadata = new Lazy<TMetadata>(() =>
            result.Satisfied switch
            {
                true => whenTrue(model, result),
                false => whenFalse(model, result)
            });

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(lazyMetadata.Value,
                result.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? []));

        var explanation = new Lazy<Explanation>(() =>
        {
            var assertions = lazyMetadata.Value switch
            {
                IEnumerable<string> because => because.ToArray(),
                _ => [Description.ToAssertion(result.Satisfied)]
            };

            return new Explanation(assertions, result.ToEnumerable(),
                result.ToEnumerable());
        });

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                result,
                expression.ToAssertion(result.Satisfied),
                Description.Statement));

        return new ExpressionTreePolicyResult<TMetadata>(
            result.Satisfied,
            lazyMetadata,
            metadataTier,
            explanation,
            resultDescription);
    }
}
