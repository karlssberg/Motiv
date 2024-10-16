using System.Linq.Expressions;

namespace Motiv.HigherOrderProposition.ExpressionTree;

internal sealed class HigherOrderFromBooleanResultMetadataExpressionTreeProposition<TModel, TMetadata>(
    Expression<Func<TModel, bool>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> whenTrue,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
    : PolicyBase<IEnumerable<TModel>, TMetadata>
{
    private readonly SpecBase<TModel, string> _spec = expression.ToSpec();

    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = EvaluateModels(models);
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = GetLazyCauses(isSatisfied, underlyingResults);
        var metadata = GetLazyMetadata(underlyingResults, causes, isSatisfied);

        return CreatePolicyResult(metadata, isSatisfied, underlyingResults, causes);
    }

    private BooleanResult<TModel, string>[] EvaluateModels(IEnumerable<TModel> models) =>
        models
            .Select(model => new BooleanResult<TModel, string>(model,  _spec.IsSatisfiedBy(model)))
            .ToArray();

    private Lazy<BooleanResult<TModel, string>[]> GetLazyCauses(
        bool isSatisfied,
        BooleanResult<TModel, string>[] underlyingResults) =>
        new(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray());

    private Lazy<TMetadata> GetLazyMetadata(
        BooleanResult<TModel, string>[] underlyingResults,
        Lazy<BooleanResult<TModel, string>[]> causes,
        bool isSatisfied)
    {
        return new Lazy<TMetadata>(() =>
        {
            var evaluation = new HigherOrderBooleanResultEvaluation<TModel, string>(
                underlyingResults,
                causes.Value);

            return isSatisfied
                ? whenTrue(evaluation)
                : whenFalse(evaluation);
        });
    }

    private PolicyResultBase<TMetadata> CreatePolicyResult(
        Lazy<TMetadata> metadata,
        bool isSatisfied,
        BooleanResult<TModel, string>[] underlyingResults,
        Lazy<BooleanResult<TModel, string>[]> causes)
    {
        var assertions = new Lazy<IEnumerable<string>>(() =>
            metadata.Value switch
            {
                IEnumerable<string>  reasons => reasons,
                _ => specDescription.ToReason(isSatisfied).ToEnumerable()
            });


        var lazyDescription = new Lazy<ResultDescriptionBase>(() =>
            new HigherOrderResultDescription<string>(
                specDescription.ToReason(isSatisfied),
                [],
                causes.Value,
                Description.Statement));

        return new HigherOrderPolicyResult<TMetadata, string>(
            isSatisfied,
            Value,
            Metadata,
            Assertions,
            ResultDescription,
            underlyingResults,
            GetCauses);

        TMetadata Value() => metadata.Value;
        IEnumerable<TMetadata> Metadata() => metadata.Value.ToEnumerable();
        IEnumerable<string> Assertions() => assertions.Value;
        ResultDescriptionBase ResultDescription() => lazyDescription.Value;
        IEnumerable<BooleanResult<TModel, string>> GetCauses() => causes.Value;
    }
}
