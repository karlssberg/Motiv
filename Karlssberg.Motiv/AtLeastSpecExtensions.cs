using Karlssberg.Motiv.AtLeast;

namespace Karlssberg.Motiv;

public static class AtLeastSpecExtensions
{
    /// <summary>
    /// Creates a new specification that requires at least a specified number of models to satisfy the given
    /// specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The base specification.</param>
    /// <param name="min">The minimum number of models that need to satisfy the specification.</param>
    /// <returns>A new specification that requires at least the specified number of models to satisfy the given specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtLeastNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int min) =>
        new AtLeastNSatisfiedSpec<TModel, TMetadata>(min, spec);

    /// <summary>
    /// Creates a new specification that requires at least a minimum number of models to satisfy the given
    /// specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The specification to be satisfied.</param>
    /// <param name="min">The minimum number of models required to satisfy the specification.</param>
    /// <param name="whenMinimumReached">The function to be executed when the minimum number of models is reached.</param>
    /// <returns>A new specification that requires at least a minimum number of models to satisfy the given specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtLeastNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int min,
        Func<IEnumerable<TModel>, TMetadata> whenMinimumReached) =>
        new AtLeastNSatisfiedSpec<TModel, TMetadata>(min, spec, whenMinimumReached);

    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtLeastNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int min,
        TMetadata whenMinimumReached) =>
        new AtLeastNSatisfiedSpec<TModel, TMetadata>(min, spec, _ => whenMinimumReached);


    /// <summary>
    /// Creates a new specification that requires at least a minimum number of models to satisfy the given
    /// specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The base specification.</param>
    /// <param name="min">The minimum number of models required to satisfy the specification.</param>
    /// <param name="whenMinimumReached">The function to execute when the minimum number of models is reached.</param>
    /// <param name="whenBlowMinimum">The function to execute when the number of models falls below the minimum.</param>
    /// <returns>A new specification that requires at least a minimum number of models to satisfy the given specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtLeastNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int min,
        Func<IEnumerable<TModel>, TMetadata> whenMinimumReached,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenBlowMinimum) =>
        new AtLeastNSatisfiedSpec<TModel, TMetadata>(
            min,
            spec,
            whenMinimumReached,
            whenBlowMinimum);

    /// <summary>
    /// Converts a single-item specification into a specification that requires at least a minimum number of items to
    /// satisfy the condition.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The single-item specification.</param>
    /// <param name="min">The minimum number of items required to satisfy the condition.</param>
    /// <param name="whenMinimumReached">The metadata to use when the minimum number of items is reached.</param>
    /// <param name="whenBlowMinimum">
    /// A function to determine the metadata to use when the number of items falls below the
    /// minimum.
    /// </param>
    /// <returns>A new specification that requires at least the specified minimum number of items to satisfy the condition.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtLeastNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int min,
        TMetadata whenMinimumReached,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenBlowMinimum) =>
        new AtLeastNSatisfiedSpec<TModel, TMetadata>(
            min,
            spec,
            _ => whenMinimumReached,
            whenBlowMinimum);

    /// <summary>
    /// Converts a single-item specification into a specification that requires at least a minimum number of items to
    /// satisfy the condition.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The single-item specification.</param>
    /// <param name="min">The minimum number of items required to satisfy the condition.</param>
    /// <param name="whenMinimumReached">The function to execute when the minimum number of items is reached.</param>
    /// <param name="whenBlowMinimum">The metadata to use when the number of items is below the minimum.</param>
    /// <returns>A new specification that requires at least the specified number of items to satisfy the condition.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtLeastNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int min,
        Func<IEnumerable<TModel>, TMetadata> whenMinimumReached,
        TMetadata whenBlowMinimum) =>
        new AtLeastNSatisfiedSpec<TModel, TMetadata>(
            min,
            spec,
            whenMinimumReached,
            _ => whenBlowMinimum);

    /// <summary>
    /// Creates a new specification that requires at least a minimum number of models to satisfy the given
    /// specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The specification to be satisfied.</param>
    /// <param name="min">The minimum number of models required to satisfy the specification.</param>
    /// <param name="whenMinimumReached">
    /// The metadata to be associated with the specification when the minimum number of models
    /// is reached.
    /// </param>
    /// <param name="whenBlowMinimum">
    /// The metadata to be associated with the specification when the number of models is below
    /// the minimum.
    /// </param>
    /// <returns>A new specification that requires at least a minimum number of models to satisfy the given specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtLeastNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int min,
        TMetadata whenMinimumReached,
        TMetadata whenBlowMinimum) =>
        new AtLeastNSatisfiedSpec<TModel, TMetadata>(
            min,
            spec,
            _ => whenMinimumReached,
            _ => whenBlowMinimum);
}