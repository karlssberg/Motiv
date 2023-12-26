using Karlssberg.Motive.Any;

namespace Karlssberg.Motive;

public static class AnySpecificationExtensions
{
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification)
    {
        return new AnySpecification<TModel, TMetadata>(specification);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory)
    {
        return new AnySpecification<TModel, TMetadata>(specification, metadataFactory);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAnyTrue)
    {
        return new AnySpecification<TModel, TMetadata>(
            specification, 
            _ => whenAnyTrue);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue)
    {
        return new AnySpecification<TModel, TMetadata>(
            specification, 
            whenAnyTrue);
    }
     
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAnyTrue,
        TMetadata whenAllFalse)
    {
        return new AnySpecification<TModel, TMetadata>(
            specification,
            _ => whenAnyTrue,
            _ => whenAllFalse);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAnyTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
    {
        return new AnySpecification<TModel, TMetadata>(
            specification,
            _ => whenAnyTrue,
            whenAllFalse);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue,
        TMetadata whenAllFalse)
    {
        return new AnySpecification<TModel, TMetadata>(
            specification,
            whenAnyTrue,
            _ => whenAllFalse);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
    {
        return new AnySpecification<TModel, TMetadata>(
            specification,
            whenAnyTrue,
            whenAllFalse);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAllTrue,
        TMetadata whenAnyTrue,
        TMetadata whenAllFalse)
    {
        return new AnySpecification<TModel, TMetadata>(
            specification,
            _ => whenAllTrue,
            _ => whenAnyTrue,
            _ => whenAllFalse);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue,
        TMetadata whenAllFalse)
    {
        return new AnySpecification<TModel, TMetadata>(
            specification,
            _ => whenAllTrue,
            whenAnyTrue,
            _ => whenAllFalse);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
    {
        return new AnySpecification<TModel, TMetadata>(
            specification,
            whenAllTrue,
            whenAnyTrue,
            whenAllFalse);
    }
}