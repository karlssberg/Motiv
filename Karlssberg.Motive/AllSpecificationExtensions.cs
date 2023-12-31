using Karlssberg.Motive.All;

namespace Karlssberg.Motive;

public static class AllSpecificationExtensions
{
    /// <summary>
    ///     Returns a specification that is satisfied when all of the models satisfy the given specification.
    /// </summary>
    /// <param name="specification">
    ///     The specification to be satisfied by all of the models.
    /// </param>
    /// <typeparam name="TModel">
    ///     The type of the model.
    /// </typeparam>
    /// <typeparam name="TMetadata">
    ///     The type of the metadata.
    /// </typeparam>
    /// <returns>
    ///     A specification that is satisfied when all of the models satisfy the given specification. Whether the specification
    ///     is satisfied or not satisfied,
    ///     the metadata is the aggregate of the underlying results
    /// </returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAllSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification) =>
        new AllSpecification<TModel, TMetadata>(specification);

    /// <summary>
    ///     Returns a specification that is satisfied when all of the models satisfy the given specification.
    /// </summary>
    /// <param name="specification">
    ///     The specification to be satisfied by all of the models.
    /// </param>
    /// <param name="metadataFactory">
    ///     A function that summarizes the underlying results of the all operation.  It is free to produce as many or as few
    ///     metadata items as it likes to describe the results of the operation.
    /// </param>
    /// <typeparam name="TModel">
    ///     The type of the model.
    /// </typeparam>
    /// <typeparam name="TMetadata">
    ///     The type of the metadata.
    /// </typeparam>
    /// <returns>
    ///     A specification that is satisfied when all of the models satisfy the given specification. The metadata is a product
    ///     of the metadata factory.
    /// </returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAllSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory) =>
        new AllSpecification<TModel, TMetadata>(specification, metadataFactory);

    /// <summary>
    ///     Returns a specification that is satisfied when all of the models satisfy the given specification. When all of the
    ///     models satisfy the specification, the metadata is the given value.
    /// </summary>
    /// <param name="specification">
    ///     The specification to be satisfied by all of the models.
    /// </param>
    /// <param name="whenAllTrue">
    ///     The metadata to be returned when all of the models satisfy the specification.
    /// </param>
    /// <typeparam name="TModel">
    ///     The type of the model.
    /// </typeparam>
    /// <typeparam name="TMetadata">
    ///     The type of the metadata.
    /// </typeparam>
    /// <returns>
    ///     A specification that is satisfied when all of the models satisfy the given specification. When all of the models
    ///     satisfy the specification, the metadata is the value of <paramref name="whenAllTrueq" />. Otherwise, the metadata
    ///     is the aggregate of the
    ///     underlying results.
    /// </returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAllSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAllTrue)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification,
            _ => whenAllTrue);
    }

    /// <summary>
    ///     Returns a specification that is satisfied when all of the models satisfy the given specification. When all of the
    ///     models satisfy the specification, the metadata is the value returned by the given function.
    /// </summary>
    /// <param name="specification">
    ///     The specification to be satisfied by all of the models.
    /// </param>
    /// <param name="whenAllTrue">
    ///     A function that returns the metadata to be returned when all of the models satisfy the specification.
    /// </param>
    /// <typeparam name="TModel">
    ///     The type of the model.
    /// </typeparam>
    /// <typeparam name="TMetadata">
    ///     The type of the metadata.
    /// </typeparam>
    /// <returns>
    ///     A specification that is satisfied when all of the models satisfy the given specification. When all of the models
    ///     satisfy the specification, the metadata is the value returned by the given function. Otherwise, the metadata is the
    ///     aggregate of the underlying results.
    /// </returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAllSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue) =>
        new AllSpecification<TModel, TMetadata>(
            specification,
            whenAllTrue);

    /// <summary>
    ///     Returns a specification that is satisfied when all of the models satisfy the given specification. When all of the
    ///     models satisfy the specification, the metadata is the value of <paramref name="whenAllTrue" />. Otherwise, the
    ///     metadata is <paramref name="whenAnyFalse" />.
    /// </summary>
    /// <param name="specification">
    ///     The specification to be satisfied by all of the models.
    /// </param>
    /// <param name="whenAllTrue">
    ///     The metadata to be returned when all of the models satisfy the specification.
    /// </param>
    /// <param name="whenAnyFalse">
    ///     The metadata to be returned for each of the models do do not satisfy the specification.
    /// </param>
    /// <typeparam name="TModel">
    ///     The type of the model.
    /// </typeparam>
    /// <typeparam name="TMetadata">
    ///     The type of the metadata.
    /// </typeparam>
    /// <returns>
    ///     A specification that is satisfied when all of the models satisfy the given specification. When all of the models
    ///     satisfy the specification, the metadata is the value of <paramref name="whenAllTrue" />. Otherwise, the metadata is
    ///     <paramref name="whenAnyFalse" />.
    /// </returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAllSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAllTrue,
        TMetadata whenAnyFalse)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification,
            _ => whenAllTrue,
            _ => whenAnyFalse);
    }

    /// <summary>
    ///     Returns a specification that is satisfied when all of the models satisfy the given specification. When all of the
    ///     models satisfy the specification, the metadata is the value of <paramref name="whenAllTrue" />. Otherwise, the
    ///     metadata is the value returned by the function <paramref name="whenAnyFalse" />.
    /// </summary>
    /// <param name="specification">
    ///     The specification to be satisfied by all of the models.
    /// </param>
    /// <param name="whenAllTrue">
    ///     The metadata to be returned when all of the models satisfy the specification.
    /// </param>
    /// <param name="whenAnyFalse">
    ///     A function that returns the metadata for each of the models do do not satisfy the specification.
    /// </param>
    /// <typeparam name="TModel">
    ///     The type of the model.
    /// </typeparam>
    /// <typeparam name="TMetadata">
    ///     The type of the metadata.
    /// </typeparam>
    /// <returns>
    ///     A specification that is satisfied when all of the models satisfy the given specification. When all of the models
    ///     satisfy the specification, the metadata is the value of <paramref name="whenAllTrue" />. Otherwise, the metadata is
    ///     the value returned by the function <paramref name="whenAnyFalse" />.
    /// </returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAllSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyFalse)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification,
            _ => whenAllTrue,
            whenAnyFalse);
    }

    /// <summary>
    ///     Returns a specification that is satisfied when all of the models satisfy the given specification. When all of the
    ///     models satisfy the specification, the metadata is the value returned by the function
    ///     <paramref name="whenAllTrue" />. Otherwise, the metadata is the value of <paramref name="whenAnyFalse" />.
    /// </summary>
    /// <param name="specification">
    ///     The specification to be satisfied by all of the models.
    /// </param>
    /// <param name="whenAllTrue">
    ///     A function that returns the metadata to be returned when all of the models satisfy the specification.
    /// </param>
    /// <param name="whenAnyFalse">
    ///     The metadata to be returned for each of the models do do not satisfy the specification.
    /// </param>
    /// <typeparam name="TModel">
    ///     The type of the model.
    /// </typeparam>
    /// <typeparam name="TMetadata">
    ///     The type of the metadata.
    /// </typeparam>
    /// <returns>
    ///     A specification that is satisfied when all of the models satisfy the given specification. When all of the models
    ///     satisfy the specification, the metadata is the value returned by the function <paramref name="whenAllTrue" />.
    ///     Otherwise, the metadata is the value of <paramref name="whenAnyFalse" />.
    /// </returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAllSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        TMetadata whenAnyFalse)
    {
        return new AllSpecification<TModel, TMetadata>(
            specification,
            whenAllTrue,
            _ => whenAnyFalse);
    }

    /// <summary>
    ///    Returns a specification that is satisfied when all of the models satisfy the given specification. When all of the models satisfy the specification, the metadata is the value returned by the function <paramref name="whenAllTrue" />. Otherwise, the metadata is the value returned by the function <paramref name="whenAnyFalse" />.
    /// </summary>
    /// <param name="specification">
    ///    The specification to be satisfied by all of the models.
    /// </param>
    /// <param name="whenAllTrue">
    ///   A function that returns the metadata to be returned when all of the models satisfy the specification.
    /// </param>
    /// <param name="whenAnyFalse">
    ///  A function that returns the metadata for each of the models do do not satisfy the specification.
    /// </param>
    /// <typeparam name="TModel">
    ///  The type of the model.
    /// </typeparam>
    /// <typeparam name="TMetadata">
    /// The type of the metadata.
    /// </typeparam>
    /// <returns>
    /// A specification that is satisfied when all of the models satisfy the given specification. When all of the models satisfy the specification, the metadata is the value returned by the function <paramref name="whenAllTrue" />. Otherwise, the metadata is the value returned by the function <paramref name="whenAnyFalse" />.
    /// </returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAllSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyFalse) =>
        new AllSpecification<TModel, TMetadata>(
            specification,
            whenAllTrue,
            whenAnyFalse);

    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAllSpecification<TModel, TMetadata>(
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

    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAllSpecification<TModel, TMetadata>(
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

    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAllSpecification<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenSomeFalse,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse) =>
        new AllSpecification<TModel, TMetadata>(
            specification,
            whenAllTrue,
            whenSomeFalse,
            whenAllFalse);
}