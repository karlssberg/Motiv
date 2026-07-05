using System.Threading;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PolicyResultPredicate;

internal sealed class HigherOrderFromPolicyResultMultiAssertionExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> whenTrue,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<string> EvaluateSpec(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);
        var causes = new Lazy<PolicyResult<TModel, TUnderlyingMetadata>[]>(() =>
            causeSelector(isSatisfied, underlyingResults)
                .ToArray(), LazyThreadSafetyMode.None);

        var metadata = new Lazy<IEnumerable<string>>(() =>
            {
                var evaluation = new HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>(
                    underlyingResults,
                    causes.Value);

                return isSatisfied
                    ? whenTrue(evaluation)
                    : whenFalse(evaluation);
            }, LazyThreadSafetyMode.None);

        var assertion = new Lazy<IEnumerable<string>>(() =>
            metadata.Value.ElseFallback(() => specDescription.ToReason(isSatisfied)), LazyThreadSafetyMode.None);

        return new HigherOrderBooleanResult<string, TUnderlyingMetadata>(
            isSatisfied,
            () => metadata.Value,
            () => assertion.Value,
            () => new HigherOrderResultDescription<TUnderlyingMetadata>(
                specDescription.ToReason(isSatisfied),
                causes.Value,
                Description.Statement),
            underlyingResults,
            () => causes.Value);
    }

    private (PolicyResult<TModel, TUnderlyingMetadata>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new PolicyResult<TModel, TUnderlyingMetadata>(model, resultResolver(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
