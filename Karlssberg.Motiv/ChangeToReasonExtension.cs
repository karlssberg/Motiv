using Karlssberg.Motiv.ChangeMetadata;
using Karlssberg.Motiv.SpecBuilder.Phase2;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for the IRequireFalseReason interface. These methods change the metadata/reasons of
/// the underlying specification and returns a new builder to complete the specification.
/// </summary>
public static class ChangeToReasonExtension
{
    /// <summary>Changes the metadata of the underlying specification when the condition is true and returns a new builder.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The specification base.</param>
    /// <param name="trueBecause">The reason when the condition is true.</param>
    /// <returns>A new builder with the changed metadata.</returns>
    public static IYieldReasonWhenFalse<TModel> YieldWhenTrue<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        string trueBecause) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec).YieldWhenTrue(trueBecause);

    /// <summary>
    /// Changes the metadata of the underlying specification when the condition is true and returns a new builder.
    /// This overload accepts a function that provides the reason when the condition is true.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The specification base.</param>
    /// <param name="trueBecause">A function that provides the reason when the condition is true.</param>
    /// <returns>A new builder with the changed metadata that solicits the corresponding reason for a false outcome.</returns>
    public static IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel> YieldWhenTrue<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        Func<string> trueBecause) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec).YieldWhenTrue(trueBecause);

    /// <summary>
    /// Changes the metadata of the underlying specification when the condition is true and returns a new builder.
    /// This overload accepts a function that takes the model and provides the reason when the condition is true.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The specification base.</param>
    /// <param name="trueBecause">A function that takes the model and provides the reason when the condition is true.</param>
    /// <returns>
    /// A new builder with the changed metadata that solicits the corresponding metadata to yield when the outcome is
    /// false.
    /// </returns>
    public static IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel> YieldWhenTrue<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        Func<TModel, string> trueBecause) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec).YieldWhenTrue(trueBecause);
}