using Karlssberg.Motive.AtLeast;

namespace Karlssberg.Motive;

public static class AtLeastSpecificationExtensions
{
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtLeastSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int min) =>
        new AtLeastNSatisfiedSpecification<TModel, TMetadata>(min, specification);

    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtLeastSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int min,
        Func<IEnumerable<TModel>, TMetadata> whenMinimumReached) =>
        new AtLeastNSatisfiedSpecification<TModel, TMetadata>(min, specification, whenMinimumReached);

    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtLeastSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int min,
        TMetadata whenMinimumReached)
    {
        return new AtLeastNSatisfiedSpecification<TModel, TMetadata>(min, specification, _ => whenMinimumReached);
    }

    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtLeastSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int min,
        Func<IEnumerable<TModel>, TMetadata> whenMinimumReached,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenBlowMinimum) =>
        new AtLeastNSatisfiedSpecification<TModel, TMetadata>(
            min,
            specification,
            whenMinimumReached,
            whenBlowMinimum);

    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtLeastSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int min,
        TMetadata whenMinimumReached,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenBlowMinimum)
    {
        return new AtLeastNSatisfiedSpecification<TModel, TMetadata>(
            min,
            specification,
            _ => whenMinimumReached,
            whenBlowMinimum);
    }

    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtLeastSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int min,
        Func<IEnumerable<TModel>, TMetadata> whenMinimumReached,
        TMetadata whenBlowMinimum)
    {
        return new AtLeastNSatisfiedSpecification<TModel, TMetadata>(
            min,
            specification,
            whenMinimumReached,
            _ => whenBlowMinimum);
    }

    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAtLeastSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        int min,
        TMetadata whenMinimumReached,
        TMetadata whenBlowMinimum)
    {
        return new AtLeastNSatisfiedSpecification<TModel, TMetadata>(
            min,
            specification,
            _ => whenMinimumReached,
            _ => whenBlowMinimum);
    }
}