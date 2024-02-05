using Karlssberg.Motiv.ChangeMetadata;
using Karlssberg.Motiv.ChangeMetadata.YieldWhenFalse;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for the IRequireFalseReason interface. These methods change the metadata/reasons of
/// the underlying specification and returns a new builder to complete the specification.
/// </summary>
public static class ChangeMetadataExtension
{
    /// <summary>Changes the metadata of the underlying specification when the condition is true and returns a new builder.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The specification base.</param>
    /// <param name="trueBecause">The reason when the condition is true.</param>
    /// <returns>A new builder with the changed metadata.</returns>
    public static IYieldReasonWhenFalse<TModel, TMetadata> YieldWhenTrue<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        string trueBecause) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec, _ => trueBecause, trueBecause);

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
    public static IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel, TMetadata> YieldWhenTrue<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        Func<TModel, string> trueBecause) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec, trueBecause);


    /// <summary>Changes the metadata of the underlying specification when the condition is true and returns a new builder.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TAltMetadata"></typeparam>
    /// <param name="spec">The specification base.</param>
    /// <param name="metadata">The reason when the condition is true.</param>
    /// <returns>A new builder with the changed metadata.</returns>
    public static IYieldMetadataWhenFalse<TModel, TAltMetadata, TMetadata> YieldWhenTrue<TModel, TMetadata, TAltMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        TAltMetadata metadata) =>
        new ChangeMetadataTypeBuilder<TModel, TAltMetadata, TMetadata>(spec, _ => metadata);


    /// <summary>
    /// Changes the metadata of the underlying specification when the condition is true and returns a new builder.
    /// This overload accepts a function that takes the model and provides the reason when the condition is true.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TAltMetadata"></typeparam>
    /// <param name="spec">The specification base.</param>
    /// <param name="metadata">A function that takes the model and provides the reason when the condition is true.</param>
    /// <returns>
    /// A new builder with the changed metadata that solicits the corresponding metadata to yield when the outcome is
    /// false.
    /// </returns>
    public static IYieldMetadataWhenFalse<TModel, TAltMetadata, TMetadata> YieldWhenTrue<TModel, TMetadata, TAltMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        Func<TModel, TAltMetadata> metadata) =>
        new ChangeMetadataTypeBuilder<TModel, TAltMetadata, TMetadata>(spec, metadata);

    /// <summary>
    /// Changes the metadata of the underlying specification when the condition is true and returns a new builder.
    /// This overload accepts a function that takes the model and provides the reason when the condition is true.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TAltMetadata"></typeparam>
    /// <param name="spec">The specification base.</param>
    /// <param name="metadata">A function that takes the model and provides the reason when the condition is true.</param>
    /// <returns>
    /// A new builder with the changed metadata that solicits the corresponding metadata to yield when the outcome is
    /// false.
    /// </returns>
    public static IYieldHigherOrderMetadataWhenFalse<TModel, TAltMetadata, TMetadata> YieldWhenTrue<TModel, TMetadata,
        TAltMetadata>(
        this SpecBase<IEnumerable<TModel>, TMetadata> spec,
        Func<IEnumerable<TModel>, IEnumerable<TModel>, TAltMetadata> metadata) =>
        new ChangeHigherOrderMetadataTypeBuilder<TModel, TAltMetadata, TMetadata>(spec, metadata);
    
    public static SpecBase<IEnumerable<TModel>, TMetadata> YieldWhenFalse<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldMetadataWhenFalse<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<TModel>, IEnumerable<TModel>, TMetadata> whenFalse) =>
        new ChangeHigherOrderMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
            builder.Spec,
            (isSatisfied, underlyingResults) =>
            {
                var underlyingResultsArray = underlyingResults.ToArray();
                var satisfied = underlyingResultsArray
                    .GetModelsWhere(result => result.IsSatisfied == isSatisfied);
                
                var unsatisfied = underlyingResultsArray
                    .GetModelsWhere(result => result.IsSatisfied != isSatisfied);

                var metadata = isSatisfied switch
                {
                    true => builder.WhenTrue(underlyingResultsArray.Select(result => result.Model)),
                    false => whenFalse(satisfied, unsatisfied)
                };

                return [metadata];
            });
    
    public static SpecBase<IEnumerable<TModel>, string> YieldWhenFalse<TModel, TMetadata>(
        this IYieldReasonWhenFalse<IEnumerable<TModel>, TMetadata> builder,
        Func<IEnumerable<TModel>, IEnumerable<TModel>, string> whenFalse) =>
        new ChangeHigherOrderMetadataSpec<TModel, string, TMetadata>(
            builder.Spec,
            (isSatisfied, underlyingResults) =>
            {
                var underlyingResultsArray = underlyingResults.ToArray();
                var satisfied = underlyingResultsArray
                    .GetModelsWhere(result => result.IsSatisfied == isSatisfied);
                
                var unsatisfied = underlyingResultsArray
                    .GetModelsWhere(result => result.IsSatisfied != isSatisfied);

                var metadata = isSatisfied switch
                {
                    true => builder.TrueBecause(underlyingResultsArray.Select(result => result.Model)),
                    false => whenFalse(satisfied, unsatisfied)
                };

                return [metadata];
            });
}