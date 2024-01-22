namespace Karlssberg.Motiv.HigherOrderSpecBuilder
{
    /// <summary>
    /// Represents a factory for creating higher order specifications.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    public interface IHigherOrderSpecFactory<TModel, TMetadata>
    {
        /// <summary>
        /// Creates a new specification with the provided description.
        /// </summary>
        /// <param name="description">The description of the specification.</param>
        /// <returns>A new instance of SpecBase with the provided description.</returns>
        SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec(string description);

        /// <summary>
        /// Creates a new specification without a description.
        /// </summary>
        /// <returns>A new instance of SpecBase without a description.</returns>
        SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec();
    }
}