using System.Threading;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.BooleanPredicate;

internal sealed class HigherOrderFromBooleanPredicateExplanationProposition<TModel>(
    Func<TModel,bool> predicate,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanEvaluation<TModel>, string> whenTrue,
    Func<HigherOrderBooleanEvaluation<TModel>, string> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
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

        var metadata = new Lazy<string>(() =>
            {
                var causes = causeSelector(isSatisfied, underlyingResults).ToArray();
                var evaluation = new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causes);

                return metadataResolver(evaluation);
            }, LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            metadata.Value.ElseFallback(() => specDescription.ToReason(isSatisfied)), LazyThreadSafetyMode.None);

        return new HigherOrderFromBooleanPredicatePolicyResult<string>(
            isSatisfied,
            () => metadata.Value,
            () => new MetadataNode<string>(metadata.Value),
            () => new Explanation(assertion.Value),
            () => new BooleanResultDescription(assertion.Value, Description.Statement));
    }

    private (ModelResult<TModel>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new ModelResult<TModel>(model, predicate(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
