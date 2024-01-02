using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv;

public static class ChangeMetadataTypeExtensions
{
    /// <summary>
    ///     Change the metadata type of a specification.
    /// </summary>
    /// <param name="specification">
    ///     The specification to change the metadata type of.
    /// </param>
    /// <param name="whenTrue">
    ///     The metadata type to change to when the specification is true.
    /// </param>
    /// <param name="whenFalse">
    ///     The metadata type to change to when the specification is false.
    /// </param>
    /// <typeparam name="TModel">
    ///     The type of the model.
    /// </typeparam>
    /// <typeparam name="TMetadata">
    ///     The type of the metadata.
    /// </typeparam>
    /// <typeparam name="TNewMetadata">
    ///     The type to change the metadata to.
    /// </typeparam>
    /// <returns>
    ///     A new specification that represents the same specification but with a different <typeparamref name="TMetadata" />.
    /// </returns>
    public static SpecificationBase<TModel, TNewMetadata> ChangeMetadata<TModel, TMetadata, TNewMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<TModel, TNewMetadata> whenTrue,
        Func<TModel, TNewMetadata> whenFalse) =>
        new ChangeMetadataTypeSpecification<TModel, TNewMetadata, TMetadata>(specification, whenTrue, whenFalse);

    /// <summary>
    ///     Change the metadata type of a specification.
    /// </summary>
    /// <param name="specification">
    ///     The specification to change the metadata type of.
    /// </param>
    /// <param name="whenTrue">
    ///     The metadata type to change to when the specification is true.
    /// </param>
    /// <param name="whenFalse">
    ///     The metadata type to change to when the specification is false.
    /// </param>
    /// <typeparam name="TModel">
    ///     The type of the model.
    /// </typeparam>
    /// <typeparam name="TMetadata">
    ///     The type of the metadata.
    /// </typeparam>
    /// <typeparam name="TNewMetadata">
    ///     The type to change the metadata to.
    /// </typeparam>
    /// <returns>
    ///     A new specification that represents the same specification but with a different <typeparamref name="TMetadata" />.
    /// </returns>
    public static SpecificationBase<TModel, TNewMetadata> ChangeMetadata<TModel, TMetadata, TNewMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TNewMetadata whenTrue,
        Func<TModel, TNewMetadata> whenFalse) =>
        new ChangeMetadataTypeSpecification<TModel, TNewMetadata, TMetadata>(specification, _ => whenTrue, whenFalse);

    /// <summary>
    ///     Change the metadata type of a specification
    /// </summary>
    /// <param name="specification">
    ///     The specification to change the metadata type of.
    /// </param>
    /// <param name="whenTrue">
    ///     The metadata type to change to when the specification is true.
    /// </param>
    /// <param name="whenFalse">
    ///     The metadata type to change to when the specification is false.
    /// </param>
    /// <typeparam name="TModel">
    ///     The type of the model.
    /// </typeparam>
    /// <typeparam name="TMetadata">
    ///     The type of the metadata.
    /// </typeparam>
    /// <typeparam name="TNewMetadata">
    ///     The type to change the metadata to.
    /// </typeparam>
    /// <returns>
    ///     A new specification that represents the same specification but with a different <typeparamref name="TMetadata" />.
    /// </returns>
    public static SpecificationBase<TModel, TNewMetadata> ChangeMetadata<TModel, TMetadata, TNewMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<TModel, TNewMetadata> whenTrue,
        TNewMetadata whenFalse) =>
        new ChangeMetadataTypeSpecification<TModel, TNewMetadata, TMetadata>(specification, whenTrue, _ => whenFalse);

    /// <summary>
    ///     Change the metadata type of a specification.
    /// </summary>
    /// <param name="specification">
    ///     The specification to change the metadata type of.
    /// </param>
    /// <param name="whenTrue">
    ///     The metadata type to change to when the specification is true.
    /// </param>
    /// <param name="whenFalse">
    ///     The metadata type to change to when the specification is false.
    /// </param>
    /// <typeparam name="TModel">
    ///     The type of the model.
    /// </typeparam>
    /// <typeparam name="TMetadata">
    ///     The type of the metadata.
    /// </typeparam>
    /// <typeparam name="TNewMetadata">
    ///     The type to change the metadata to.
    /// </typeparam>
    /// <returns>
    ///     A new specification that represents the same specification but with a different <typeparamref name="TMetadata" />.
    /// </returns>
    public static SpecificationBase<TModel, TNewMetadata> ChangeMetadata<TModel, TMetadata, TNewMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TNewMetadata whenTrue,
        TNewMetadata whenFalse) =>
        new ChangeMetadataTypeSpecification<TModel, TNewMetadata, TMetadata>(specification, _ => whenTrue, _ => whenFalse);
}