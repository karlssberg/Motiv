using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive.Any;

public sealed class AnySpecification<TModel, TMetadata>(
    SpecificationBase<TModel, TMetadata> underlyingSpecification,
    Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory)
    : SpecificationBase<IEnumerable<TModel>, TMetadata>
{
    internal AnySpecification(SpecificationBase<TModel, TMetadata> specification) : this(specification, SelectCauses)
    {
    }
    
    internal AnySpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<BooleanResultWithModel<TModel,TMetadata>, TMetadata> whenAnyTrue) 
        : this(underlyingSpecification, CreateMetadataFactory(whenAnyTrue))
    {
    }
    
    internal AnySpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<BooleanResultWithModel<TModel,TMetadata>, TMetadata> whenAnyTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse) 
        : this(underlyingSpecification, CreateMetadataFactory(whenAnyTrue, whenAllFalse))
    {
    } 
    
    internal AnySpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel,TMetadata>, TMetadata> whenSomeTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse) 
        : this(underlyingSpecification, CreateMetadataFactory(whenAllTrue, whenSomeTrue, whenAllFalse))
    {
    }
    
    public SpecificationBase<TModel, TMetadata> UnderlyingSpecification { get; } = Throw.IfNull(underlyingSpecification, nameof(underlyingSpecification));

    public override BooleanResultBase<TMetadata> Evaluate(IEnumerable<TModel> models)
    {
        var results = models
            .Select(model => 
                WrapThrownExceptions(
                    this,
                    UnderlyingSpecification,
                    () =>
                    {
                        var result1 = UnderlyingSpecification.Evaluate(model);
                        return new BooleanResultWithModel<TModel, TMetadata>(model, result1);
                    }))
            .ToList();

        return new AnyBooleanResult<TMetadata>(ResolveMetadata, results.Select(result => result.UnderlyingResult));

        IEnumerable<TMetadata> ResolveMetadata(bool isSatisfied) =>
            metadataFactory(isSatisfied, results);
    }

    public override string Description { get; } = $"ANY({underlyingSpecification})";

    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel,TMetadata>, TMetadata> whenAnyTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
    {
        return (isSatisfied, results) =>
        {
            if (!isSatisfied)
                return [whenAllFalse(results.Select(result => result.Model))];
            
            var resultsList = results.ToList();
            return resultsList.All(result => result.IsSatisfied)
                ? [whenAllTrue(resultsList.Select(result => result.Model))]
                : resultsList.Where(result => result.IsSatisfied).Select(whenAnyTrue);
        };
    }
    
    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<BooleanResultWithModel<TModel,TMetadata>, TMetadata> whenAnyTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
    {
        return (isSatisfied, results) => isSatisfied
            ? results
                .Where(result => result.IsSatisfied)
                .Select(whenAnyTrue)
            : [whenAllFalse(results.Select(result => result.Model))];
    }

    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue)
    {
        return (isSatisfied, results) => isSatisfied
            ? results
                .Where(result => result.IsSatisfied)
                .Select(whenAnyTrue)
            : results.SelectMany(result => result.Causes);
    }

    private static IEnumerable<TMetadata> SelectCauses(bool isSatisfied, IEnumerable<BooleanResultWithModel<TModel, TMetadata>> results) =>
        results
            .Where(result => result.IsSatisfied == isSatisfied)
            .SelectMany(result => result.Causes);
    
    private string GetCaller()
    {
        var genericArgs = GetType().GetGenericArguments();
        return $"{nameof(AnySpecification<TModel, TMetadata>)}<{genericArgs.First()}, {genericArgs.Last()}>.{nameof(Evaluate)}()";
    }
}