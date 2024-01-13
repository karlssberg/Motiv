using static Karlssberg.Motiv.SpecException;

namespace Karlssberg.Motiv.AtLeast;

internal sealed class AtLeastNSatisfiedSpec<TModel, TMetadata>(
    int minimum,
    SpecBase<TModel, TMetadata> underlyingSpec,
    Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory)
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
        : this(minimum, underlyingSpec, CreateMetadataFactory(whenTrue))
    {
    }

    internal AtLeastNSatisfiedSpec(
        int minimum,
        SpecBase<TModel, TMetadata> underlyingSpec,
        Func<IEnumerable<TModel>, TMetadata> whenTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenMinimumExceeded)
        : this(minimum, underlyingSpec, CreateMetadataFactory(whenTrue, whenMinimumExceeded))
    {
    }

    public override string Description => $"AT_LEAST_{minimum}({underlyingSpec})";

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

        return new AtLeastNSatisfiedBooleanResult<TMetadata>(minimum, ResolveMetadata, results.Select(result => result.UnderlyingResult));

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