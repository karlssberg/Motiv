using Karlssberg.Motiv.All;
using Karlssberg.Motiv.Any;
using Karlssberg.Motiv.AtLeast;
using Karlssberg.Motiv.AtMost;
using Karlssberg.Motiv.ChangeMetadataType;
using Karlssberg.Motiv.Range;
using Karlssberg.Motiv.Exactly;

namespace Karlssberg.Motiv;

public static class HigherOrderSpecExtensions
{
    /// <summary>
    /// Returns a specification that is a transformation of the <paramref name="spec" /> into one that is satisfied
    /// when any of the models in a collection are satisfied.
    /// </summary>
    /// <param name="spec">The underlying specification that is to be converted into a new any-satisfied specification.</param>
    /// <param name="description"></param>
    /// <typeparam name="TModel">
    /// The type of the model.  This will end up as the collection-type of the resultant
    /// specification.
    /// </typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>
    /// A new specification that is satisfied when any of the models in the collection satisfy the
    /// <paramref name="spec" />. Whether the specification is satisfied or not satisfied, the metadata is the aggregate of the
    /// underlying results
    /// </returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> CreateAnySpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        string? description = null) =>
        new AnySpec<TModel, TMetadata>(spec, description);

    /// <summary>
    /// Returns a specification that is a transformation of the <paramref name="spec" /> into one that is satisfied
    /// when all of the models in a collection are satisfied.
    /// </summary>
    /// <param name="spec">The underlying specification that is to be converted into a new all-satisfied specification.</param>
    /// <param name="description">The proposition as a textual statement</param>
    /// <typeparam name="TModel">
    /// The type of the model.  This will end up as the collection-type of the resultant
    /// specification.
    /// </typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>
    /// A new specification that is satisfied when all of the models in the collection satisfy the
    /// <paramref name="spec" />. Whether the specification is satisfied or not satisfied, the metadata is the aggregate of the
    /// underlying results
    /// </returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> CreateAllSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        string? description = null) =>
        new AllSpec<TModel, TMetadata>(spec, description);
    
    /// <summary>
    /// Creates a specification that is satisfied if the underlying specification is satisfied by at least
    /// <paramref name="n" /> models.
    /// </summary>
    /// <param name="spec">The underlying specification.</param>
    /// <param name="n">The number of models that must satisfy the underlying specification.</param>
    /// <param name="description">An optional description of the specification.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>
    /// A specification that is satisfied if the underlying specification is satisfied by at least
    /// <paramref name="n" /> models.
    /// </returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> CreateExactlySpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int n,
        string? description = null) =>
        new ExactlySpec<TModel, TMetadata>(n, spec, description);

    /// <summary>
    /// Creates a new specification that requires at least a specified number of models to satisfy the given
    /// specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The base specification.</param>
    /// <param name="min">The minimum number of models that need to satisfy the specification.</param>
    /// <returns>A new specification that requires at least the specified number of models to satisfy the given specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> CreateAtLeastSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int min) =>
        new AtLeastSpec<TModel, TMetadata>(min, spec);

    /// <summary>Converts a specification into an "at most N satisfied" specification.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The original specification.</param>
    /// <param name="max">The maximum number of satisfied conditions.</param>
    /// <returns>An "at most N satisfied" specification.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> CreateAtMostSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int max) =>
        new AtMostSpec<TModel, TMetadata>(max, spec);

    /// <summary>
    /// Creates a new specification that requires the number of models that satisfy the given specification to be
    /// between the specified minimum and maximum values.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="spec">The base specification.</param>
    /// <param name="min">The minimum number of models that need to satisfy the specification.</param>
    /// <param name="max">The maximum number of models that need to satisfy the specification.</param>
    /// <returns>
    /// A new specification that requires the number of models that satisfy the given specification to be between the
    /// specified minimum and maximum values.
    /// </returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> CreateRangeSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int min,
        int max) =>
        new RangeSpec<TModel, TMetadata>(
            min.ThrowIfLessThan(0, nameof(min)),
            max.ThrowIfLessThan(min, nameof(max)), 
            spec);
}