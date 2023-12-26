namespace Karlssberg.Motive;

public static class EnumerableExtensions
{
    public static BooleanResultBase<TMetadata> Any<TModel, TMetadata>(
        this IEnumerable<TModel> source, 
        SpecificationBase<TModel, TMetadata> specification)
    {
        return specification.ToAnySpecification().Evaluate(source);
    }
    
    public static BooleanResultBase<TMetadata> All<TModel, TMetadata>(
        this IEnumerable<TModel> source, 
        SpecificationBase<TModel, TMetadata> specification)
    {
        return specification.ToAnySpecification().Evaluate(source);
    }
    
    public static IEnumerable<T> ElseIfEmpty<T>(this IEnumerable<T> source, IEnumerable<T> other)
    {
        var isEmpty = true;
        foreach (var item in source)
        {
            yield return item;
            isEmpty = false;
        }

        if (!isEmpty) yield break;
        
        foreach (var item in other)
            yield return item;
    }
}