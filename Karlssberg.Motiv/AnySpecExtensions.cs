using Karlssberg.Motiv.Any;

namespace Karlssberg.Motiv;

public static class AnySatisfiedSpecExtensions
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
    public static AnySatisfiedSpec<TModel, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        string? description = null) =>
        new(spec, description);
}