using Karlssberg.Motiv.All;
using Karlssberg.Motiv.HigherOrderSpecBuilder;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

namespace Karlssberg.Motiv;

public static class AllSatisfiedSpecExtensions
{
    /// <summary>
    /// Returns a specification that is a transformation of the <paramref name="spec" /> into one that is satisfied
    /// when all of the models in a collection are satisfied.
    /// </summary>
    /// <param name="spec">The underlying specification that is to be converted into a new all-satisfied specification.</param>
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
    public static AllSatisfiedSpec<TModel, TMetadata> ToAllSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec) =>
        new(spec, new MetadataFactory<TModel, TMetadata, TMetadata>());

    /// <summary>
    /// Commences the building of a specification that is a transformation of the <paramref name="spec" /> into one
    /// that is satisfied when all of the models in a collection are satisfied.
    /// </summary>
    /// <param name="spec">The underlying specification that is to be converted into a new all-satisfied specification.</param>
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
    public static IYieldMetadataWhenTrue<TModel, TMetadata, TMetadata> BuildAllSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec) =>
        new AllSatisfiedSpecBuilder<TModel, TMetadata, TMetadata>(spec);
}