using System.Threading;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.BooleanPredicate;

internal sealed class HigherOrderFromBooleanPredicateMultiMetadataProposition<TModel, TMetadata>(
    Func<TModel, bool> predicate,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<TMetadata> EvaluateSpec(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        var lazyMetadata = new Lazy<IEnumerable<TMetadata>>(() =>
            {
                var causes = causeSelector(isSatisfied, underlyingResults).ToArray();
                var evaluation = new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causes);

                return isSatisfied
                    ? whenTrue(evaluation)
                    : whenFalse(evaluation);
            }, LazyThreadSafetyMode.None);

        var lazyAssertion = new Lazy<IEnumerable<string>>(() =>
            lazyMetadata.Value switch
            {
                IEnumerable<string> because => because,
                _ => specDescription.ToReason(isSatisfied).ToEnumerable()
            }, LazyThreadSafetyMode.None);

        return new HigherOrderFromBooleanPredicateBooleanResult<TMetadata>(
            isSatisfied,
            () => new MetadataNode<TMetadata>(lazyMetadata.Value, []),
            () => new Explanation(lazyAssertion.Value),
            () => new BooleanResultDescription(
                specDescription.ToReason(isSatisfied),
                Description.Statement,
                lazyAssertion.Value));
    }

    private (ModelResult<TModel>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = models.Select(model => new ModelResult<TModel>(model, predicate(model))).ToArray();
        return (results, higherOrderPredicate(results));
    }
}
