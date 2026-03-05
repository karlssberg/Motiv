using System.Linq.Expressions;
using System.Threading;
using Motiv.BooleanPredicateProposition;
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

    public override bool Matches(TModel model) => _predicate.Match(model);

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var result = _predicate.Execute(model);
        BooleanResultBase<string>[] resultArray = [result];

        var lazyMetadata = new Lazy<TMetadata>(() =>
            result.Satisfied switch
            {
                true => whenTrue(model, result),
                false => whenFalse(model, result)
            }, LazyThreadSafetyMode.None);

        return new PropositionPolicyResult<TMetadata>(
            result.Satisfied,
            () => lazyMetadata.Value,
            () => new MetadataNode<TMetadata>(lazyMetadata.Value,
                resultArray as IEnumerable<BooleanResultBase<TMetadata>> ?? []),
            () =>
            {
                var assertions = lazyMetadata.Value switch
                {
                    IEnumerable<string> because => because.ToArray(),
                    _ => result.Assertions
                };

                return new Explanation(assertions, resultArray,
                    resultArray);
            },
            () => new ExpressionTreeBooleanResultDescription(
                result,
                description.ToReason(result.Satisfied),
                expression,
                Description.Statement));
    }
}
