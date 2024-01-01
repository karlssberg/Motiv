using Karlssberg.Motive.AtMost;

namespace Karlssberg.Motive;

public static class AtMostSpecificationExtensions
{
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtMostSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int max)
    {
        return new AtMostNSatisfiedSpecification<TModel, TMetadata>(max, specification);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtMostSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int max,
        Func<IEnumerable<TModel>, TMetadata> whenWithinLimit)
    {
        return new AtMostNSatisfiedSpecification<TModel, TMetadata>(max, specification, whenWithinLimit);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtMostSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int max,
        TMetadata whenWithinLimit)
    {
        return new AtMostNSatisfiedSpecification<TModel, TMetadata>(max, specification, _ => whenWithinLimit);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtMostSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int max,
        Func<IEnumerable<TModel>, TMetadata> whenWithinLimit,
        Func<BooleanResultWithModel<TModel,TMetadata>, TMetadata> whenMaximumExceeded)
    {
        return new AtMostNSatisfiedSpecification<TModel, TMetadata>(max, specification, whenWithinLimit, whenMaximumExceeded);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtMostSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int max,
        TMetadata whenWithinLimit,
        Func<BooleanResultWithModel<TModel,TMetadata>, TMetadata> whenMaximumExceeded)
    {
        return new AtMostNSatisfiedSpecification<TModel, TMetadata>(max, specification, _ => whenWithinLimit, whenMaximumExceeded);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtMostSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int max,
        Func<IEnumerable<TModel>, TMetadata> whenWithinLimit,
        TMetadata whenMaximumExceeded)
    {
        return new AtMostNSatisfiedSpecification<TModel, TMetadata>(max, specification, whenWithinLimit, _ => whenMaximumExceeded);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtMostSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int max,
        TMetadata whenWithinLimit,
        TMetadata whenMaximumExceeded)
    {
        return new AtMostNSatisfiedSpecification<TModel, TMetadata>(max, specification, _ => whenWithinLimit, _ => whenMaximumExceeded);
    } 
}