using Karlssberg.Motiv.SubstituteMetadata;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for substituting metadata in specifications.
/// </summary>
public static class SubstituteMetadataSpecificationsExtensions
{
    /// <summary>
    /// Substitutes the metadata of a specification with the provided functions.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The specification to substitute metadata for.</param>
    /// <param name="whenTrue">The function to retrieve the metadata when the specification is true.</param>
    /// <param name="whenFalse">The function to retrieve the metadata when the specification is false.</param>
    /// <returns>A new specification with substituted metadata.</returns>
    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(specification, whenTrue, whenFalse);

    /// <summary>
    /// Substitutes the metadata of a specification with the provided value and function.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The specification to substitute metadata for.</param>
    /// <param name="whenTrue">The value of the metadata when the specification is true.</param>
    /// <param name="whenFalse">The function to retrieve the metadata when the specification is false.</param>
    /// <returns>A new specification with substituted metadata.</returns>
    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(specification, _ => whenTrue, whenFalse);

    /// <summary>
    /// Substitutes the metadata of a specification with the provided function and value.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The specification to substitute metadata for.</param>
    /// <param name="whenTrue">The function to retrieve the metadata when the specification is true.</param>
    /// <param name="whenFalse">The value of the metadata when the specification is false.</param>
    /// <returns>A new specification with substituted metadata.</returns>
    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(specification, whenTrue, _ => whenFalse);

    /// <summary>
    /// Substitutes the metadata of a specification with the provided values.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The specification to substitute metadata for.</param>
    /// <param name="whenTrue">The value of the metadata when the specification is true.</param>
    /// <param name="whenFalse">The value of the metadata when the specification is false.</param>
    /// <returns>A new specification with substituted metadata.</returns>
    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenTrue,
        TMetadata whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(specification, _ => whenTrue, _ => whenFalse);

    /// <summary>
    /// Substitutes the metadata of a specification with the provided functions and description.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The specification to substitute metadata for.</param>
    /// <param name="description">The description of the new specification.</param>
    /// <param name="whenTrue">The function to retrieve the metadata when the specification is true.</param>
    /// <param name="whenFalse">The function to retrieve the metadata when the specification is false.</param>
    /// <returns>A new specification with substituted metadata.</returns>
    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        string description,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(description, specification, whenTrue, whenFalse);

    /// <summary>
    /// Substitutes the metadata of a specification with the provided value and function, along with a description.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The specification to substitute metadata for.</param>
    /// <param name="description">The description of the new specification.</param>
    /// <param name="whenTrue">The value of the metadata when the specification is true.</param>
    /// <param name="whenFalse">The function to retrieve the metadata when the specification is false.</param>
    /// <returns>A new specification with substituted metadata.</returns>
    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        string description,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(description, specification, _ => whenTrue, whenFalse);

    /// <summary>
    /// Substitutes the metadata of a specification with the provided function and value, along with a description.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The specification to substitute metadata for.</param>
    /// <param name="description">The description of the new specification.</param>
    /// <param name="whenTrue">The function to retrieve the metadata when the specification is true.</param>
    /// <param name="whenFalse">The value of the metadata when the specification is false.</param>
    /// <returns>A new specification with substituted metadata.</returns>
    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        string description,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(description, specification, whenTrue, _ => whenFalse);

    /// <summary>
    /// Substitutes the metadata of a specification with the provided values, along with a description.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The specification to substitute metadata for.</param>
    /// <param name="description">The description of the new specification.</param>
    /// <param name="whenTrue">The value of the metadata when the specification is true.</param>
    /// <param name="whenFalse">The value of the metadata when the specification is false.</param>
    /// <returns>A new specification with substituted metadata.</returns>
    public static SpecificationBase<TModel, TMetadata> SubstituteMetadata<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        string description,
        TMetadata whenTrue,
        TMetadata whenFalse) =>
        new SubstituteMetadataSpecification<TModel, TMetadata>(description, specification, _ => whenTrue, _ => whenFalse);
}