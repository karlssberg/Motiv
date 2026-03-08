using System.Threading;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.BooleanPredicate;

internal sealed class HigherOrderFromBooleanPredicateProposition<TModel, TMetadata>(
    Func<TModel,bool> predicate,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue,
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
    : PolicyBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);
        var metadataResolver = isSatisfied
            ? whenTrue
            : whenFalse;

        var metadata = new Lazy<TMetadata>(() =>
            {
                var causes = causeSelector(isSatisfied, underlyingResults).ToArray();
                var evaluation = new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causes);

                return metadataResolver(evaluation);
            }, LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            metadata.Value switch
            {
                string because => because,
                _ => specDescription.ToReason(isSatisfied)
            }, LazyThreadSafetyMode.None);

        var reason = new Lazy<string>(() =>
            metadata.Value switch
            {
                string because when !Description.HasExplicitStatement => because,
                _ => specDescription.ToReason(isSatisfied)
            }, LazyThreadSafetyMode.None);

        return new HigherOrderFromBooleanPredicatePolicyResult<TMetadata>(
            isSatisfied,
            () => metadata.Value,
            () => new MetadataNode<TMetadata>(metadata.Value),
            () => new Explanation(assertion.Value),
            () => new BooleanResultDescription(reason.Value, Description.Statement));
    }

    private (ModelResult<TModel>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new ModelResult<TModel>(model, predicate(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
