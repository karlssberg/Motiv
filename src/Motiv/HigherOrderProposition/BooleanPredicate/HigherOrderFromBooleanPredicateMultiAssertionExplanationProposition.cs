using System.Threading;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.BooleanPredicate;

internal sealed class HigherOrderFromBooleanPredicateMultiAssertionExplanationProposition<TModel>(
    Func<TModel, bool> predicate,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> whenTrue,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        EvaluateModels(models).IsSatisfied;

    protected override BooleanResultBase<string> EvaluateSpec(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        var lazyMetadata = new Lazy<IEnumerable<string>>(() =>
            {
                var causes = causeSelector(isSatisfied, underlyingResults).ToArray();
                var evaluation = new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causes);

                return isSatisfied
                    ? whenTrue(evaluation)
                    : whenFalse(evaluation);
            }, LazyThreadSafetyMode.None);

        var lazyAssertion = new Lazy<IEnumerable<string>>(() =>
            lazyMetadata.Value.ElseFallback(() => specDescription.ToReason(isSatisfied)), LazyThreadSafetyMode.None);

        return new HigherOrderFromBooleanPredicateBooleanResult<string>(
            isSatisfied,
            () => new MetadataNode<string>(lazyMetadata.Value, []),
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
