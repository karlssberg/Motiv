using System.Threading;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.BooleanResultPredicate;

internal sealed class HigherOrderFromBooleanResultExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, string> whenTrue,
    Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : PolicyBase<IEnumerable<TModel>, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override PolicyResultBase<string> EvaluatePolicy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);
        var metadataResolver = isSatisfied
            ? whenTrue
            : whenFalse;

        var causes = new Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray(), LazyThreadSafetyMode.None);

        var metadata = new Lazy<string>(() =>
            {
                var evaluation = new HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>(
                    underlyingResults,
                    causes.Value);

                return metadataResolver(evaluation);
            }, LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            metadata.Value.ElseFallback(() => specDescription.ToReason(isSatisfied)), LazyThreadSafetyMode.None);

        return new HigherOrderPolicyResult<string, TUnderlyingMetadata>(
            isSatisfied,
            () => metadata.Value,
            () => metadata.Value.ToEnumerable(),
            () => assertion.Value.ToEnumerable(),
            () => new HigherOrderResultDescription<TUnderlyingMetadata>(
                assertion.Value,
                causes.Value,
                Description.Statement),
            underlyingResults,
            () => causes.Value);
    }

    private (BooleanResult<TModel, TUnderlyingMetadata>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new BooleanResult<TModel, TUnderlyingMetadata>(model, resultResolver(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
