namespace Karlssberg.Motiv.All;

internal sealed class AllSatisfiedSpecification<TModel, TMetadata>(
    SpecificationBase<TModel, TMetadata> underlyingSpecification,
    Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory)
    : SpecificationBase<IEnumerable<TModel>, TMetadata>
{
    internal AllSatisfiedSpecification(SpecificationBase<TModel, TMetadata> specification)
        : this(
            specification,
            (_, _) => Enumerable.Empty<TMetadata>())
    {
    }

    internal AllSatisfiedSpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue)
        : this(underlyingSpecification, CreateMetadataFactory(whenAllTrue))
    {
    }

    internal AllSatisfiedSpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyFalse)
        : this(underlyingSpecification, CreateMetadataFactory(whenAllTrue, whenAnyFalse))
    {
    }

    internal AllSatisfiedSpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyFalse,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
        : this(underlyingSpecification, CreateMetadataFactory(whenAllTrue, whenAnyFalse, whenAllFalse))
    {
    }

    public override string Description => $"ALL({underlyingSpecification})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var resultsWithModel = models
            .Select(model => 
                WrapException.IfIsSatisfiedByInvocationFails(this, underlyingSpecification,
                    () =>
                    {
                        var underlyingResult = underlyingSpecification.IsSatisfiedBy(model);
                        return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
                    }))
            .ToList();

        return new AllSatisfiedBooleanResult<TMetadata>(
            isSatisfied => metadataFactory(isSatisfied, resultsWithModel), 
            resultsWithModel.Select(result => result.UnderlyingResult));
    }

    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyFalse,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
    {
        return (isSatisfied, results) =>
        {
            if (isSatisfied)
                return [whenAllTrue(results.Select(result => result.Model))];

            var resultsList = results.ToList();
            return resultsList.All(result => !result.IsSatisfied)
                ? [whenAllFalse(resultsList.Select(result => result.Model))]
                : resultsList.Select(whenAnyFalse);
        };
    }

    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyFalse)
    {
        return (isSatisfied, results) => isSatisfied
            ? [whenAllTrue(results.Select(result => result.Model))]
            : results.Select(whenAnyFalse);
    }

    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue)
    {
        return (isSatisfied, results) => isSatisfied
            ? [whenAllTrue(results.Select(result => result.Model))]
            : results
                .Where(result => !result.IsSatisfied)
                .SelectMany(result => result.GetInsights());
    }
}