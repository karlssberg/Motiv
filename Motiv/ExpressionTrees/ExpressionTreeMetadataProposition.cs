using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTrees;

internal sealed class ExpressionTreeMetadataProposition<TModel, TMetadata>(
    Expression<Func<TModel, bool>> expression,
    Func<TModel, BooleanResultBase<string>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<string>, TMetadata> whenFalse,
    string statement)
    : PolicyBase<TModel, TMetadata>
{
    private readonly Lazy<ISpecDescription> _description = new (() => new SpecDescription(statement));
    public override IEnumerable<SpecBase> Underlying => UnderlyingSpec.ToEnumerable();

    private readonly Func<TModel, bool> _predicate = expression.Compile();

    public SpecBase<TModel, string> UnderlyingSpec { get; } = expression.ToSpec();

    public override ISpecDescription Description => _description.Value;

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(TModel model)
    {
        var satisfied = _predicate(model);

        var lazyBooleanResult = new Lazy<BooleanResultBase<string>>(() => UnderlyingSpec.IsSatisfiedBy(model));

        var lazyMetadata = new Lazy<TMetadata>(() =>
            satisfied switch
            {
                true => whenTrue(model, lazyBooleanResult.Value),
                false => whenFalse(model, lazyBooleanResult.Value)
            });

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(lazyMetadata.Value,
                lazyBooleanResult.Value.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? []));

        var explanation = new Lazy<Explanation>(() =>
        {
            var assertions = lazyMetadata.Value switch
            {
                IEnumerable<string> because => because.ToArray(),
                _ => [Description.ToReason(satisfied)]
            };

            return new Explanation(assertions, lazyBooleanResult.Value.ToEnumerable(),
                lazyBooleanResult.Value.ToEnumerable());
        });

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
            new BooleanResultDescriptionWithUnderlying(
                lazyBooleanResult.Value,
                Description.ToReason(satisfied),
                Description.Statement));

        return new ExpressionTreePolicyResult<TMetadata>(
            satisfied,
            lazyMetadata,
            metadataTier,
            explanation,
            resultDescription);
    }
}
