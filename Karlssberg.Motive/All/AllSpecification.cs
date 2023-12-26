using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive.All;

internal sealed class AllSpecification<TModel, TMetadata>
    : SpecificationBase<IEnumerable<TModel>, TMetadata>
{
    private readonly Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> _metadataFactory;

    internal AllSpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory)
    {
        UnderlyingSpecification = underlyingSpecification;
        Description = $"ALL({underlyingSpecification})";
        _metadataFactory = metadataFactory;
    }
    
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

    public SpecificationBase<TModel, TMetadata> UnderlyingSpecification { get; } 

    public override string Description { get; }

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
            _metadataFactory(isSatisfied, results);
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