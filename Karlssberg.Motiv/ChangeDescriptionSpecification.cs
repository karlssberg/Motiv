namespace Karlssberg.Motiv;

/// <summary>
/// Represents a specification that changes the description of an underlying specification.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal class ChangeDescriptionSpecification<TModel, TMetadata>(
    string description,
    SpecificationBase<TModel, TMetadata> underlyingSpecification) : SpecificationBase<TModel, TMetadata>
{
    /// <summary>
    /// Gets the description of the specification.
    /// </summary>
    public override string Description => description;

    /// <summary>
    /// Determines if the model satisfies the specification.
    /// </summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>A Boolean result indicating if the model satisfies the specification.</returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) =>
        underlyingSpecification.IsSatisfiedBy(model);
}