namespace Karlssberg.Motiv.AtLeast;

internal sealed class AtLeastNSatisfiedSpec<TModel, TMetadata>(
    int minimum,
    SpecBase<TModel, TMetadata> underlyingSpec,
    Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactoryFn)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    internal AtLeastNSatisfiedSpec(int minimum, SpecBase<TModel, TMetadata> spec)
        : this(minimum, spec, SelectCauses)
    {
    }

    internal AtLeastNSatisfiedSpec(
        int minimum,
        SpecBase<TModel, TMetadata> underlyingSpec,
        Func<IEnumerable<TModel>, TMetadata> whenTrue)
        : this(minimum, underlyingSpec, CreatemetadataFactoryFn(whenTrue))
    {
    }

    internal AtLeastNSatisfiedSpec(
        int minimum,
        SpecBase<TModel, TMetadata> underlyingSpec,
        Func<IEnumerable<TModel>, TMetadata> whenTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenMinimumExceeded)
        : this(minimum, underlyingSpec, CreatemetadataFactoryFn(whenTrue, whenMinimumExceeded))
    {
    }

    public override string Description => $"AT_LEAST_{minimum}({underlyingSpec})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var results = models
            .Select(model =>
            {
                var underlyingResult = underlyingSpec.IsSatisfiedByOrWrapException(model);
                return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
            })
            .ToArray();

        var isSatisfied = results.Count(result => result.IsSatisfied) >= minimum;
        return new AtLeastNSatisfiedBooleanResult<TMetadata>(
            isSatisfied,
            minimum, 
            metadataFactoryFn(isSatisfied, results),
            results.Select(result => result.UnderlyingResult));

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