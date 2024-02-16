using Karlssberg.Motiv.ChangeFalseMetadata;
using Karlssberg.Motiv.ChangeMetadataType;
using Karlssberg.Motiv.ChangeMetadataType.YieldWhenFalse;
using Karlssberg.Motiv.ChangeTrueMetadata;

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
//    public static IYieldReasonWhenFalse<IEnumerable<TModel>, TMetadata> YieldWhenTrue<TModel, TMetadata>(
//        this SpecBase<IEnumerable<TModel>, TMetadata> spec,
//        string trueBecause) =>
//        new ChangeMetadataBuilder<IEnumerable<TModel>, TMetadata>(spec, _ => trueBecause, trueBecause);

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
//    public static IYieldReasonWithDescriptionUnresolvedWhenFalse<IEnumerable<TModel>, TMetadata> YieldWhenTrue<TModel, TMetadata>(
//        this SpecBase<IEnumerable<TModel>, TMetadata> spec,
//        Func<IEnumerable<TModel>, string> trueBecause) =>
//        new ChangeMetadataBuilder<IEnumerable<TModel>, TMetadata>(spec, trueBecause);

//
//    /// <summary>Changes the metadata of the underlying specification when the condition is true and returns a new builder.</summary>
//    /// <typeparam name="TModel">The type of the model.</typeparam>
//    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
//    /// <typeparam name="TAltMetadata"></typeparam>
//    /// <param name="spec">The specification base.</param>
//    /// <param name="metadata">The reason when the condition is true.</param>
//    /// <returns>A new builder with the changed metadata.</returns>
//    public static IYieldMetadataWhenFalse<IEnumerable<TModel>, TAltMetadata, TMetadata> WhenTrue<TModel, TMetadata, TAltMetadata>(
//        this SpecBase<IEnumerable<TModel>, TMetadata> spec,
//        TAltMetadata metadata) =>
//        new ChangeMetadataTypeBuilder<IEnumerable<TModel>, TAltMetadata, TMetadata>(spec, _ => metadata);

//
//    /// <summary>
//    /// Changes the metadata of the underlying specification when the condition is true and returns a new builder.
//    /// This overload accepts a function that takes the model and provides the reason when the condition is true.
//    /// </summary>
//    /// <typeparam name="TModel">The type of the model.</typeparam>
//    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
//    /// <typeparam name="TAltMetadata"></typeparam>
//    /// <param name="spec">The specification base.</param>
//    /// <param name="metadata">A function that takes the model and provides the reason when the condition is true.</param>
//    /// <returns>
//    /// A new builder with the changed metadata that solicits the corresponding metadata to yield when the outcome is
//    /// false.
//    /// </returns>
//    public static IYieldMetadataWhenFalse<IEnumerable<TModel>, TAltMetadata, TMetadata> WhenTrue<TModel, TMetadata, TAltMetadata>(
//        this SpecBase<IEnumerable<TModel>, TMetadata> spec,
//        Func<IEnumerable<TModel>, TAltMetadata> metadata) =>
//        new ChangeMetadataTypeBuilder<IEnumerable<TModel>, TAltMetadata, TMetadata>(spec, metadata);

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
    
//    public static SpecBase<TModel, TMetadata> WhenTrue<TModel, TMetadata>(
//        this SpecBase<TModel, TMetadata> spec,
//        Func<TModel, TMetadata> whenTrue) =>
//        new ChangeTrueMetadataSpec<TModel, TMetadata>(spec, whenTrue);
//    
//    public static SpecBase<TModel, TMetadata> WhenTrue<TModel, TMetadata>(
//        this SpecBase<TModel, TMetadata> spec,
//        TMetadata whenTrue) =>
//            whenTrue switch
//            {
//                string description => new ChangeTrueMetadataSpec<TModel, TMetadata>(spec, _ => whenTrue, description),
//                _ => new ChangeTrueMetadataSpec<TModel, TMetadata>(spec, _ => whenTrue)
//            };
//    
//    public static SpecBase<TModel, TMetadata> YieldWhenFalse<TModel, TMetadata>(
//        this SpecBase<TModel, TMetadata> spec,
//        Func<TModel, TMetadata> whenFalse) =>
//        new ChangeFalseMetadataSpec<TModel, TMetadata>(spec, whenFalse);
//    
//    public static SpecBase<TModel, TMetadata> WhenFalse<TModel, TMetadata>(
//        this SpecBase<TModel, TMetadata> spec,
//        TMetadata whenTrue) =>
//        new ChangeFalseMetadataSpec<TModel, TMetadata>(spec, _ => whenTrue);
//
//    public static SpecBase<IEnumerable<TModel>, TMetadata> YieldWhenFalse<TModel, TMetadata, TUnderlyingMetadata>(
//        this IYieldMetadataWhenFalse<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> builder,
//        Func<IEnumerable<TModel>, IEnumerable<TModel>, TMetadata> whenFalse) =>
//        new ChangeHigherOrderMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
//            builder.Spec,
//            (isSatisfied, underlyingResults) =>
//            {
//                var underlyingResultsArray = underlyingResults.ToArray();
//                var satisfied = underlyingResultsArray
//                    .GetModelsWhere(result => result.Satisfied == isSatisfied);
//                
//                var unsatisfied = underlyingResultsArray
//                    .GetModelsWhere(result => result.Satisfied != isSatisfied);
//
//                var metadata = isSatisfied switch
//                {
//                    true => builder.WhenTrue(underlyingResultsArray.Select(result => result.Model)),
//                    false => whenFalse(satisfied, unsatisfied)
//                };
//
//                return [metadata];
//            });
//    
//    public static SpecBase<IEnumerable<TModel>, string> YieldWhenFalse<TModel, TMetadata>(
//        this IYieldReasonWhenFalse<IEnumerable<TModel>, TMetadata> builder,
//        Func<IEnumerable<TModel>, IEnumerable<TModel>, string> whenFalse) =>
//        new ChangeHigherOrderMetadataSpec<TModel, string, TMetadata>(
//            builder.Spec,
//            (isSatisfied, underlyingResults) =>
//            {
//                var underlyingResultsArray = underlyingResults.ToArray();
//                var satisfied = underlyingResultsArray
//                    .GetModelsWhere(result => result.Satisfied == isSatisfied);
//                
//                var unsatisfied = underlyingResultsArray
//                    .GetModelsWhere(result => result.Satisfied != isSatisfied);
//
//                var metadata = isSatisfied switch
//                {
//                    true => builder.TrueBecause(underlyingResultsArray.Select(result => result.Model)),
//                    false => whenFalse(satisfied, unsatisfied)
//                };
//
//                return [metadata];
//            });
}