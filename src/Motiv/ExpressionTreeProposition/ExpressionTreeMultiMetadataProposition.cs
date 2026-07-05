using System.Linq.Expressions;

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

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var result = _predicate.Execute(model);

        var metadataResolver =
            result.Satisfied switch
            {
                true => whenTrue,
                false => whenFalse
            };

        return new ExpressionTreeMultiMetadataPropositionBooleanResult<TModel, TMetadata, TPredicateResult>(
            result.Satisfied,
            model,
            result,
            metadataResolver,
            expression,
            description);
    }
}
