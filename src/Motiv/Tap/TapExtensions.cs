namespace Motiv;

/// <summary>
/// Provides extension methods for attaching side-effects to specifications without altering their logical behavior.
/// </summary>
public static class TapExtensions
{
    /// <summary>
    /// Wraps a specification so that a callback fires on every evaluation.
    /// The callback receives the model and the result, but the result itself is returned unchanged.
    /// </summary>
    /// <param name="spec">The specification to wrap.</param>
    /// <param name="callback">The callback to invoke on every evaluation.</param>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <typeparam name="TMetadata">The metadata type.</typeparam>
    /// <returns>A new specification that transparently wraps the original, firing the callback on evaluation.</returns>
    public static SpecBase<TModel, TMetadata> Tap<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        Action<TModel, BooleanResultBase<TMetadata>> callback) =>
        new Tap.TapSpec<TModel, TMetadata>(spec, callback);

    /// <summary>
    /// Wraps a specification so that a callback fires only when the evaluation is satisfied.
    /// </summary>
    /// <param name="spec">The specification to wrap.</param>
    /// <param name="callback">The callback to invoke when the result is satisfied.</param>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <typeparam name="TMetadata">The metadata type.</typeparam>
    /// <returns>A new specification that transparently wraps the original, firing the callback when satisfied.</returns>
    public static SpecBase<TModel, TMetadata> TapWhenTrue<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        Action<TModel, BooleanResultBase<TMetadata>> callback) =>
        new Tap.TapWhenTrueSpec<TModel, TMetadata>(spec, callback);

    /// <summary>
    /// Wraps a specification so that a callback fires only when the evaluation is not satisfied.
    /// </summary>
    /// <param name="spec">The specification to wrap.</param>
    /// <param name="callback">The callback to invoke when the result is not satisfied.</param>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <typeparam name="TMetadata">The metadata type.</typeparam>
    /// <returns>A new specification that transparently wraps the original, firing the callback when not satisfied.</returns>
    public static SpecBase<TModel, TMetadata> TapWhenFalse<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        Action<TModel, BooleanResultBase<TMetadata>> callback) =>
        new Tap.TapWhenFalseSpec<TModel, TMetadata>(spec, callback);
}
