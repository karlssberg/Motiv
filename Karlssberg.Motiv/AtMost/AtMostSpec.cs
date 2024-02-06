
namespace Karlssberg.Motiv.AtMost;

internal sealed class AtMostSpec<TModel, TMetadata>(
    int maximum,
    SpecBase<TModel, TMetadata> underlyingSpec,
    string? description = null)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override string Description => description switch
    {
        null => $"AT_MOST_{maximum}({underlyingSpec})",
        not null => $"<{description}>({underlyingSpec})"
    };

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var results = models
            .Select(model =>
            {
                var underlyingResult = underlyingSpec.IsSatisfiedByOrWrapException(model);
                return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
            })
            .ToArray();
        
        var isSatisfied = results.Count(result => result.Value) <= maximum;
        return new AtMostBooleanResult<TMetadata>(
            isSatisfied,
            maximum,
            results.Select(result => result.UnderlyingResult)); ;
    }

    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>>
        CreatemetadataFactoryFn(
            Func<IEnumerable<TModel>, TMetadata> whenTrue,
            Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyFalse)
    {
        return (isSatisfied, results) => isSatisfied
            ? [whenTrue(results.Select(result => result.Model))]
            : results.Select(whenAnyFalse);
    }

    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>>
        CreatemetadataFactoryFn(
            Func<IEnumerable<TModel>, TMetadata> whenTrue)
    {
        return (isSatisfied, results) => isSatisfied
            ? [whenTrue(results.Select(result => result.Model))]
            : results
                .Where(result => !result.Value)
                .SelectMany(result => result.GetMetadata());
    }

    private static IEnumerable<TMetadata> SelectCauses(bool isSatisfied,
        IEnumerable<BooleanResultWithModel<TModel, TMetadata>> results) =>
        results
            .Where(result => result.Value == isSatisfied)
            .SelectMany(result => result.GetMetadata());
}