
namespace Karlssberg.Motiv.AtMost;

internal sealed class AtMostNSatisfiedSpec<TModel, TMetadata>(
    int maximum,
    SpecBase<TModel, TMetadata> underlyingSpec,
    Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactoryFn,
    string? description = null)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    internal AtMostNSatisfiedSpec(int maximum, SpecBase<TModel, TMetadata> spec)
        : this(maximum, spec, SelectCauses)
    {
    }

    internal AtMostNSatisfiedSpec(
        int maximum,
        SpecBase<TModel, TMetadata> underlyingSpec,
        Func<IEnumerable<TModel>, TMetadata> whenTrue)
        : this(maximum, underlyingSpec, CreatemetadataFactoryFn(whenTrue))
    {
    }

    internal AtMostNSatisfiedSpec(
        int maximum,
        SpecBase<TModel, TMetadata> underlyingSpec,
        Func<IEnumerable<TModel>, TMetadata> whenTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenMaximumExceeded)
        : this(maximum, underlyingSpec, CreatemetadataFactoryFn(whenTrue, whenMaximumExceeded))
    {
    }

    public override string Description => description switch
    {
        null => $"AT_MOST_{maximum}({underlyingSpec})",
        _ => $"<{description}>({underlyingSpec})"
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
        
        var isSatisfied = results.Count(result => result.IsSatisfied) <= maximum;
        return new AtMostNSatisfiedBooleanResult<TMetadata>(
            isSatisfied,
            maximum,
            metadataFactoryFn(isSatisfied, results),
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
                .Where(result => !result.IsSatisfied)
                .SelectMany(result => result.GetMetadata());
    }

    private static IEnumerable<TMetadata> SelectCauses(bool isSatisfied,
        IEnumerable<BooleanResultWithModel<TModel, TMetadata>> results) =>
        results
            .Where(result => result.IsSatisfied == isSatisfied)
            .SelectMany(result => result.GetMetadata());
}