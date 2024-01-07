using Karlssberg.Motiv.Any;

namespace Karlssberg.Motiv;

public static class AnySpecificationExtensions
{
    /// <summary>Converts a specification into an "AnySatisfied" specification that operates on a collection of models.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The original specification.</param>
    /// <returns>An "AnySatisfied" specification that operates on a collection of models.</returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification) =>
        new AnySatisfiedSpecification<TModel, TMetadata>(specification);


    /// <summary>
    ///     Converts the given specification into an <see cref="AnySatisfiedSpecification{TModel,TMetadata}" /> that
    ///     checks if any item satisfies the specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The original specification to convert.</param>
    /// <param name="metadataFactory">The factory function that generates metadata based on the evaluation results.</param>
    /// <returns>An instance of <see cref="AnySatisfiedSpecification{TModel, TMetadata}" />.</returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory) =>
        new AnySatisfiedSpecification<TModel, TMetadata>(specification, metadataFactory);

    /// <summary>
    ///     Converts a single-item specification into a specification that is satisfied when any item in a collection
    ///     satisfies the original specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model being evaluated.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata associated with the model.</typeparam>
    /// <param name="specification">The original specification.</param>
    /// <param name="whenAnyTrue">
    ///     The metadata to be associated with the collection when any item satisfies the original
    ///     specification.
    /// </param>
    /// <returns>A new specification that is satisfied when any item in a collection satisfies the original specification.</returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAnyTrue)
    {
        return new AnySatisfiedSpecification<TModel, TMetadata>(
            specification,
            _ => whenAnyTrue);
    }

    /// <summary>Converts the given specification into an "AnySatisfiedSpecification" that operates on an enumerable of models.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The original specification to convert.</param>
    /// <param name="whenAnyTrue">The function to be executed when any model satisfies the specification.</param>
    /// <returns>An instance of the "AnySatisfiedSpecification" class.</returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue) =>
        new AnySatisfiedSpecification<TModel, TMetadata>(
            specification,
            whenAnyTrue);

    /// <summary>Converts a single specification into an "AnySatisfied" specification that operates on a collection of models.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The original specification.</param>
    /// <param name="whenAnyTrue">The metadata to be applied when any of the models satisfy the specification.</param>
    /// <param name="whenAllFalse">The metadata to be applied when none of the models satisfy the specification.</param>
    /// <returns>An "AnySatisfied" specification that operates on a collection of models.</returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAnyTrue,
        TMetadata whenAllFalse)
    {
        return new AnySatisfiedSpecification<TModel, TMetadata>(
            specification,
            _ => whenAnyTrue,
            _ => whenAllFalse);
    }

    /// <summary>
    ///     Converts a single-item specification into a specification that is satisfied if any item in a collection
    ///     satisfies the original specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The original specification.</param>
    /// <param name="whenAnyTrue">
    ///     The metadata to be associated with the specification when any item satisfies the original
    ///     specification.
    /// </param>
    /// <param name="whenAllFalse">
    ///     A function that returns the metadata to be associated with the specification when none of
    ///     the items satisfy the original specification.
    /// </param>
    /// <returns>A new specification that is satisfied if any item in a collection satisfies the original specification.</returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAnyTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
    {
        return new AnySatisfiedSpecification<TModel, TMetadata>(
            specification,
            _ => whenAnyTrue,
            whenAllFalse);
    }

    /// <summary>
    ///     Converts a single model specification into a specification that is satisfied if any of the models satisfy the
    ///     original specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The original specification.</param>
    /// <param name="whenAnyTrue">The metadata to use when any of the models satisfy the specification.</param>
    /// <param name="whenAllFalse">The metadata to use when none of the models satisfy the specification.</param>
    /// <returns>A new specification that is satisfied if any of the models satisfy the original specification.</returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue,
        TMetadata whenAllFalse)
    {
        return new AnySatisfiedSpecification<TModel, TMetadata>(
            specification,
            whenAnyTrue,
            _ => whenAllFalse);
    }

    /// <summary>Creates a new specification that checks if any of the models satisfy the given specification.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The specification to be checked.</param>
    /// <param name="whenAnyTrue">The function to be executed when any model satisfies the specification.</param>
    /// <param name="whenAllFalse">The function to be executed when none of the models satisfy the specification.</param>
    /// <returns>A new specification that checks if any of the models satisfy the given specification.</returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse) =>
        new AnySatisfiedSpecification<TModel, TMetadata>(
            specification,
            whenAnyTrue,
            whenAllFalse);

    /// <summary>
    ///     Converts a single specification into an "AnySatisfiedSpecification" that evaluates whether any of the items in
    ///     a collection satisfy the original specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The original specification to convert.</param>
    /// <param name="whenAllTrue">The metadata to use when all items in the collection satisfy the original specification.</param>
    /// <param name="whenAnyTrue">
    ///     The metadata to use when at least one item in the collection satisfies the original
    ///     specification.
    /// </param>
    /// <param name="whenAllFalse">
    ///     The metadata to use when none of the items in the collection satisfy the original
    ///     specification.
    /// </param>
    /// <returns>An instance of the "AnySatisfiedSpecification" class.</returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAllTrue,
        TMetadata whenAnyTrue,
        TMetadata whenAllFalse)
    {
        return new AnySatisfiedSpecification<TModel, TMetadata>(
            specification,
            _ => whenAllTrue,
            _ => whenAnyTrue,
            _ => whenAllFalse);
    }

    /// <summary>
    ///     Converts a specification into an "Any Satisfied" specification, which checks if any of the conditions are
    ///     satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The original specification.</param>
    /// <param name="whenAllTrue">The metadata to use when all conditions are true.</param>
    /// <param name="whenAnyTrue">The function to determine the metadata when any condition is true.</param>
    /// <param name="whenAllFalse">The metadata to use when all conditions are false.</param>
    /// <returns>The "Any Satisfied" specification.</returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue,
        TMetadata whenAllFalse)
    {
        return new AnySatisfiedSpecification<TModel, TMetadata>(
            specification,
            _ => whenAllTrue,
            whenAnyTrue,
            _ => whenAllFalse);
    }

    /// <summary>Creates a new specification that checks if any of the models satisfy the given specification.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specification">The specification to be satisfied.</param>
    /// <param name="whenAllTrue">The function to be executed when all models satisfy the specification.</param>
    /// <param name="whenAnyTrue">The function to be executed when any model satisfies the specification.</param>
    /// <param name="whenAllFalse">The function to be executed when none of the models satisfy the specification.</param>
    /// <returns>A new specification that checks if any of the models satisfy the given specification.</returns>
    public static SpecificationBase<IEnumerable<TModel>, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse) =>
        new AnySatisfiedSpecification<TModel, TMetadata>(
            specification,
            whenAllTrue,
            whenAnyTrue,
            whenAllFalse);
}