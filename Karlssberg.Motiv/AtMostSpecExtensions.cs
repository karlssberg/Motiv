using Karlssberg.Motiv.AtMost;

namespace Karlssberg.Motiv;

public static class AtMostSpecExtensions
{
    /// <summary>Converts a specification into an "at most N satisfied" specification.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The original specification.</param>
    /// <param name="max">The maximum number of satisfied conditions.</param>
    /// <returns>An "at most N satisfied" specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtMostNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int max) =>
        new AtMostNSatisfiedSpec<TModel, TMetadata>(max, spec);

    /// <summary>Creates a specification that allows at most a specified number of models to satisfy the given specification.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The specification to apply.</param>
    /// <param name="max">The maximum number of models allowed to satisfy the specification.</param>
    /// <param name="whenWithinLimit">A function that generates metadata when the number of models is within the limit.</param>
    /// <returns>A new specification that allows at most the specified number of models to satisfy the given specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtMostNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int max,
        Func<IEnumerable<TModel>, TMetadata> whenWithinLimit) =>
        new AtMostNSatisfiedSpec<TModel, TMetadata>(max, spec, whenWithinLimit);

    /// <summary>Creates a specification that allows at most a specified number of models to satisfy the given specification.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The specification to apply.</param>
    /// <param name="max">The maximum number of models that can satisfy the specification.</param>
    /// <param name="whenWithinLimit">The metadata to apply when the number of satisfied models is within the limit.</param>
    /// <returns>A specification that allows at most a specified number of models to satisfy the given specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtMostNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int max,
        TMetadata whenWithinLimit)
    {
        return new AtMostNSatisfiedSpec<TModel, TMetadata>(max, spec, _ => whenWithinLimit);
    }

    /// <summary>
    ///     Creates a new specification that allows at most a specified number of models to satisfy the given
    ///     specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The base specification.</param>
    /// <param name="max">The maximum number of models allowed to satisfy the specification.</param>
    /// <param name="whenWithinLimit">The function to execute when the number of models is within the limit.</param>
    /// <param name="whenMaximumExceeded">The function to execute when the maximum number of models is exceeded.</param>
    /// <returns>A new specification that allows at most a specified number of models to satisfy the given specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtMostNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int max,
        Func<IEnumerable<TModel>, TMetadata> whenWithinLimit,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenMaximumExceeded) =>
        new AtMostNSatisfiedSpec<TModel, TMetadata>(max, spec, whenWithinLimit, whenMaximumExceeded);

    /// <summary>Converts the given specification into an "at most N satisfied" specification.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The original specification.</param>
    /// <param name="max">The maximum number of satisfied conditions.</param>
    /// <param name="whenWithinLimit">The metadata to use when the number of satisfied conditions is within the limit.</param>
    /// <param name="whenMaximumExceeded">
    ///     The function to determine the metadata when the number of satisfied conditions
    ///     exceeds the limit.
    /// </param>
    /// <returns>An "at most N satisfied" specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtMostNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int max,
        TMetadata whenWithinLimit,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenMaximumExceeded)
    {
        return new AtMostNSatisfiedSpec<TModel, TMetadata>(max, spec, _ => whenWithinLimit, whenMaximumExceeded);
    }

    /// <summary>Creates a specification that allows at most N models to satisfy the given specification.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The base specification.</param>
    /// <param name="max">The maximum number of models allowed to satisfy the specification.</param>
    /// <param name="whenWithinLimit">The metadata to use when the number of models is within the limit.</param>
    /// <param name="whenMaximumExceeded">The metadata to use when the maximum number of models is exceeded.</param>
    /// <returns>A new specification that allows at most N models to satisfy the given specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtMostNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int max,
        Func<IEnumerable<TModel>, TMetadata> whenWithinLimit,
        TMetadata whenMaximumExceeded)
    {
        return new AtMostNSatisfiedSpec<TModel, TMetadata>(max, spec, whenWithinLimit, _ => whenMaximumExceeded);
    }

    /// <summary>Creates a specification that allows at most N models to satisfy the given specification.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The specification to apply.</param>
    /// <param name="max">The maximum number of models allowed to satisfy the specification.</param>
    /// <param name="whenWithinLimit">The metadata to apply when the number of models is within the limit.</param>
    /// <param name="whenMaximumExceeded">The metadata to apply when the number of models exceeds the limit.</param>
    /// <returns>A new specification that allows at most N models to satisfy the given specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToAtMostNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int max,
        TMetadata whenWithinLimit,
        TMetadata whenMaximumExceeded)
    {
        return new AtMostNSatisfiedSpec<TModel, TMetadata>(max, spec, _ => whenWithinLimit, _ => whenMaximumExceeded);
    }
}