using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTrees;

internal sealed class ExpressionTreeMultiMetadataProposition<TModel, TMetadata>(
    Expression<Func<TModel, bool>> expression,
    Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> whenFalse,
    string statement)
    : SpecBase<TModel, TMetadata>
{
    private readonly Lazy<ISpecDescription> _description = new (() => new SpecDescription(statement));
    public override IEnumerable<SpecBase> Underlying => UnderlyingSpec.ToEnumerable();

    private readonly Func<TModel, bool> _predicate = expression.Compile();

    public SpecBase<TModel, string> UnderlyingSpec { get; } = expression.ToSpec();

    public override ISpecDescription Description => _description.Value;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model)
    {
        var satisfied = _predicate(model);
        var booleanResult = UnderlyingSpec.IsSatisfiedBy(model);

        var metadata = new Lazy<IEnumerable<TMetadata>>(() =>
            satisfied switch
            {
                true => whenTrue(model, booleanResult),
                false => whenFalse(model, booleanResult)
            });

        var metadataTier = new Lazy<MetadataNode<TMetadata>>(() =>
            new MetadataNode<TMetadata>(metadata.Value,
                booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? []));

        var explanation = new Lazy<Explanation>(() =>
        {
            var assertions = metadata.Value switch
            {
                IEnumerable<string> because => because.ToArray(),
                _ => [Description.ToReason(satisfied)]
            };
            return new Explanation(assertions, booleanResult.ToEnumerable(), booleanResult.ToEnumerable());
        });

        var resultDescription = new Lazy<ResultDescriptionBase>(() =>
        {
            var trueOrFalse = satisfied ? "true" : "false";
            return new BooleanResultDescriptionWithUnderlying(
                booleanResult,
                $"{Description.Statement} == {trueOrFalse}",
                Description.Statement);
        });

        return new ExpressionTreeBooleanResult<TMetadata>(
            satisfied,
            metadataTier,
            explanation,
            resultDescription);
    }
}
