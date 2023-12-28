using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive.All;

internal sealed class AllSpecification<TModel, TMetadata>(
    SpecificationBase<TModel, TMetadata> underlyingSpecification,
    Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory) : SpecificationBase<IEnumerable<TModel>, TMetadata>
{
    internal AllSpecification(SpecificationBase<TModel, TMetadata> specification) 
        : this(
            specification, 
            (_, _) => Enumerable.Empty<TMetadata>())
    {
    }
    
    internal AllSpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue) 
        : this(underlyingSpecification, CreateMetadataFactory(whenAllTrue))
    {
    } 
    
    internal AllSpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel,TMetadata>, TMetadata> whenAnyFalse) 
        : this(underlyingSpecification, CreateMetadataFactory(whenAllTrue, whenAnyFalse))
    {
    } 
    
    internal AllSpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel,TMetadata>, TMetadata> whenAnyFalse,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse) 
        : this(underlyingSpecification, CreateMetadataFactory(whenAllTrue, whenAnyFalse, whenAllFalse))
    {
    }

    public SpecificationBase<TModel, TMetadata> UnderlyingSpecification { get; } = Throw.IfNull(underlyingSpecification, nameof(underlyingSpecification));

    public override string Description => $"All({underlyingSpecification})";

    public override BooleanResultBase<TMetadata> Evaluate(IEnumerable<TModel> models) 
    {
        var results = models
            .Select(model => 
                WrapThrownExceptions(
                    this,
                    UnderlyingSpecification,
                    () =>
                    {
                        var underlyingResult = UnderlyingSpecification.Evaluate(model);
                        return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
                    }))
            .ToList();

        return new AllBooleanResult<TMetadata>(ResolveMetadata, results.Select(result => result.UnderlyingResult));

        IEnumerable<TMetadata> ResolveMetadata(bool isSatisfied) =>
            metadataFactory(isSatisfied, results);
    }

    private static Func<bool,IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel,TMetadata>, TMetadata> whenAnyFalse,
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
    
    private static Func<bool,IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel,TMetadata>, TMetadata> whenAnyFalse)
    {
        return (isSatisfied, results) => isSatisfied 
            ? [whenAllTrue(results.Select(result => result.Model))] 
            : results.Select(whenAnyFalse);
    }
    
    private static Func<bool,IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue)
    {
        return (isSatisfied, results) => isSatisfied
            ? [whenAllTrue(results.Select(result => result.Model))]
            : results
                .Where(result => !result.IsSatisfied)
                .SelectMany(result => result.GetInsights());
    }
}