using static Karlssberg.Motiv.SpecException;

namespace Karlssberg.Motiv.AtMost;

internal sealed class AtMostNSatisfiedSpec<TModel, TMetadata>(
    int maximum,
    SpecBase<TModel, TMetadata> underlyingSpec,
    Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory)
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
        : this(maximum, underlyingSpec, CreateMetadataFactory(whenTrue))
    {
    }

    internal AtMostNSatisfiedSpec(
        int maximum,
        SpecBase<TModel, TMetadata> underlyingSpec,
        Func<IEnumerable<TModel>, TMetadata> whenTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenMaximumExceeded)
        : this(maximum, underlyingSpec, CreateMetadataFactory(whenTrue, whenMaximumExceeded))
    {
    }

    public override string Description => $"AT_MOST_{maximum}({underlyingSpec})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var results = models
            .Select(
                model =>
                    WrapException.IfIsSatisfiedByInvocationFails(
                        this,
                        underlyingSpec,
                        () =>
                        {
                            var underlyingResult = underlyingSpec.IsSatisfiedBy(model);
                            return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
                        }))
            .ToList();

        return new AtMostNSatisfiedBooleanResult<TMetadata>(maximum, ResolveMetadata, results.Select(result => result.UnderlyingResult));

        IEnumerable<TMetadata> ResolveMetadata(bool isSatisfied)
        {
            return metadataFactory(isSatisfied, results);
        }
    }

    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<IEnumerable<TModel>, TMetadata> whenTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyFalse)
    {
        return (isSatisfied, results) => isSatisfied
            ? [whenTrue(results.Select(result => result.Model))]
            : results.Select(whenAnyFalse);
    }

    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<IEnumerable<TModel>, TMetadata> whenTrue)
    {
        return (isSatisfied, results) => isSatisfied
            ? [whenTrue(results.Select(result => result.Model))]
            : results
                .Where(result => !result.IsSatisfied)
                .SelectMany(result => result.GetMetadata());
    }

    private static IEnumerable<TMetadata> SelectCauses(bool isSatisfied, IEnumerable<BooleanResultWithModel<TModel, TMetadata>> results) =>
        results
            .Where(result => result.IsSatisfied == isSatisfied)
            .SelectMany(result => result.GetMetadata());
}