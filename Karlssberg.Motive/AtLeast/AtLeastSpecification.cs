using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive.AtLeast;

internal sealed class AtLeastSpecification<TModel, TMetadata>(
    int minimum,
    SpecificationBase<TModel, TMetadata> underlyingSpecification,
    Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory)
    : SpecificationBase<IEnumerable<TModel>, TMetadata>
{
    internal AtLeastSpecification(int minimum, SpecificationBase<TModel, TMetadata> specification)
        : this(minimum, specification, SelectCauses)
    {
    }

    internal AtLeastSpecification(
        int minimum,
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<IEnumerable<TModel>, TMetadata> whenTrue)
        : this(minimum, underlyingSpecification, CreateMetadataFactory(whenTrue))
    {
    }

    internal AtLeastSpecification(
        int minimum,
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<IEnumerable<TModel>, TMetadata> whenTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenMinimumExceeded)
        : this(minimum, underlyingSpecification, CreateMetadataFactory(whenTrue, whenMinimumExceeded))
    {
    }

    public override string Description => $"AT_LEAST_{minimum}({underlyingSpecification})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var results = models
            .Select(
                model =>
                    WrapException.IfIsSatisfiedByInvocationFails(
                        this,
                        underlyingSpecification,
                        () =>
                        {
                            var underlyingResult = underlyingSpecification.IsSatisfiedBy(model);
                            return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
                        }))
            .ToList();

        return new AtLeastBooleanResult<TMetadata>(minimum, ResolveMetadata, results.Select(result => result.UnderlyingResult));

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
                .SelectMany(result => result.GetInsights());
    }

    private static IEnumerable<TMetadata> SelectCauses(bool isSatisfied, IEnumerable<BooleanResultWithModel<TModel, TMetadata>> results) =>
        results
            .Where(result => result.IsSatisfied == isSatisfied)
            .SelectMany(result => result.GetInsights());
}