using Karlssberg.Motive.All;

namespace Karlssberg.Motive;

public static class AllSpecificationExtensions
{
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> All<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification)
    {
        return new AllSpecification<TModel, TMetadata>(specification);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> All<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory)
    {
        return new AllSpecification<TModel, TMetadata>(specification, metadataFactory);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> All<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAllTrue)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification, 
            _ => whenAllTrue);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> All<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification,
            whenAllTrue);
    }

    public static SpecificationBase<IEnumerable<TModel>, TMetadata> All<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAllTrue,
        TMetadata whenAnyFalse)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification,
            _ => whenAllTrue,
            _ => whenAnyFalse);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> All<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyFalse)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification,
            _ => whenAllTrue,
            whenAnyFalse);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> All<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        TMetadata whenAnyFalse)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification,
             whenAllTrue,
            _ => whenAnyFalse);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> All<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyFalse)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification,
            whenAllTrue,
            whenAnyFalse);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> All<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAllTrue,
        TMetadata whenSomeFalse,
        TMetadata whenAllFalse)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification,
            _ => whenAllTrue,
            _ => whenSomeFalse,
            _ => whenAllFalse);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> All<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenSomeFalse,
        TMetadata whenAllFalse)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification,
            _ => whenAllTrue,
            whenSomeFalse,
            _ => whenAllFalse);
    }
    
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> All<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenSomeFalse,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification,
            whenAllTrue,
            whenSomeFalse,
            whenAllFalse);
    }
}