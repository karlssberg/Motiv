using Karlssberg.Motiv.SubstituteMetadata;

namespace Karlssberg.Motiv;

public static class SubstituteMetadataSpecificationsExtensions
{
    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(specification, whenTrue, whenFalse);

    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(specification, _ => whenTrue, whenFalse);

    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(specification, whenTrue, _ => whenFalse);

    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenTrue,
        TMetadata whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(specification, _ => whenTrue, _ => whenFalse);
    
    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        string description,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(description, specification, whenTrue, whenFalse);

    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        string description,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(description, specification, _ => whenTrue, whenFalse);

    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        string description,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(description, specification, whenTrue, _ => whenFalse);

    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        string description,
        TMetadata whenTrue,
        TMetadata whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(description, specification, _ => whenTrue, _ => whenFalse);
}