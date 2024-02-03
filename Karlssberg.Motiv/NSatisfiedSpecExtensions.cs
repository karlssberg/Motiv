using Karlssberg.Motiv.NSatisfied;

namespace Karlssberg.Motiv;

public static class NSatisfiedSpecExtensions
{
    /// <summary>
    ///     Creates a specification that is satisfied if the underlying specification is satisfied by at least <paramref name="n" />
    ///     models.
    /// </summary>
    /// <param name="spec">The underlying specification.</param>
    /// <param name="n">The number of models that must satisfy the underlying specification.</param>
    /// <param name="description">An optional description of the specification.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A specification that is satisfied if the underlying specification is satisfied by at least <paramref name="n" /> models.</returns>
    public static SpecBase<IEnumerable<TModel>, TMetadata> ToNSatisfiedSpec<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        int n,
        string? description = null) =>
        new NSatisfiedSpec<TModel, TMetadata>(n, spec, description);
}