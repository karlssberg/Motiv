using Karlssberg.Motiv.Any;
using Karlssberg.Motiv.HigherOrderSpecBuilder;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

namespace Karlssberg.Motiv;

public static class AnySatisfiedSpecExtensions
{
    /// <summary>
    /// Returns a specification that is a transformation of the <paramref name="spec" /> into one that is satisfied
    /// when any of the models in a collection are satisfied.
    /// </summary>
    /// <param name="spec">The underlying specification that is to be converted into a new any-satisfied specification.</param>
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
    public static AnySatisfiedSpec<TModel, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec) =>
        new(spec, DefaultMetadataFactory.GetFactory<TModel, TMetadata>());

    /// <summary>
    /// Commences the building of a specification that is a transformation of the <paramref name="spec" /> into one
    /// that is satisfied when any of the models in a collection are satisfied.
    /// </summary>
    /// <param name="spec">The underlying specification that is to be converted into a new any-satisfied specification.</param>
    /// <typeparam name="TModel">
    /// The type of the model.  This will end up as the collection-type of the resultant
    /// specification.
    /// </typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the metadata associated with the underlying specification.</typeparam>
    /// <returns>
    /// A new specification that is satisfied when any of the models in the collection satisfy the
    /// <paramref name="spec" />. Whether the specification is satisfied or not satisfied, the metadata is the aggregate of the
    /// underlying results
    /// </returns>
    public static IYieldAllTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> BuildAnySatisfiedSpec<TModel, TMetadata,
        TUnderlyingMetadata>(
        this SpecBase<TModel, TUnderlyingMetadata> spec) =>
        new AnySatisfiedSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(spec);

    /// <summary>
    /// Commences the building of a specification that is a transformation of the <paramref name="spec" /> into one
    /// that is satisfied when any of the models in a collection are satisfied.
    /// </summary>
    /// <param name="spec">The underlying specification that is to be converted into a new any-satisfied specification.</param>
    /// <typeparam name="TModel">
    /// The type of the model.  This will end up as the collection-type of the resultant
    /// specification.
    /// </typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the metadata associated with the underlying specification.</typeparam>
    /// <returns>
    /// A new specification that is satisfied when any of the models in the collection satisfy the
    /// <paramref name="spec" />. Whether the specification is satisfied or not satisfied, the metadata is the aggregate of the
    /// underlying results
    /// </returns>
    public static IYieldAllTrueReasons<TModel, TUnderlyingMetadata> BuildAnySatisfiedSpec<TModel, TUnderlyingMetadata>(
        this SpecBase<TModel, TUnderlyingMetadata> spec) =>
        new AnySatisfiedSpecBuilder<TModel, TUnderlyingMetadata>(spec);
}