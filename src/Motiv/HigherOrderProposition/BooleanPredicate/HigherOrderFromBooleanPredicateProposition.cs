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

    protected override PolicyResultBase<TMetadata> IsPolicySatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new ModelResult<TModel>(model, predicate(model)))
            .ToArray();
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var metadataResolver = isSatisfied
            ? whenTrue
            : whenFalse;

        var metadata = new Lazy<TMetadata>(() =>
            {
                var causes = causeSelector(isSatisfied, underlyingResults).ToArray();
                var evaluation = new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causes);

                return metadataResolver(evaluation);
            });

        var assertion = new Lazy<string>(() =>
            metadata.Value switch
            {
                string because => because,
                _ => specDescription.ToReason(isSatisfied)
            });

        var resultDescription = new Lazy<BooleanResultDescription>(() =>
            new BooleanResultDescription(specDescription.ToReason(isSatisfied), Description.Statement));

        return new HigherOrderFromBooleanPredicatePolicyResult<TMetadata>(
            isSatisfied,
            () => metadata.Value,
            () => new MetadataNode<TMetadata>(metadata.Value, []),
            () => new Explanation(assertion.Value),
            () => resultDescription.Value);
    }
}
