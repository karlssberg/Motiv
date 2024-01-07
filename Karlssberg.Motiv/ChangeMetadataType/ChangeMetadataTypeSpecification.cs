namespace Karlssberg.Motiv.ChangeMetadataType;

internal class ChangeMetadataTypeSpecification<TModel, TMetadata, TUnderlyingMetadata>(
    string description,
    SpecificationBase<TModel, TUnderlyingMetadata> underlyingSpecification,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse) : SpecificationBase<TModel, TMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeMetadataTypeSpecification{TModel, TMetadata, TOtherMetadata}"/> class.
    /// </summary>
    /// <param name="underlyingSpecification">The underlying specification to be evaluated.</param>
    /// <param name="whenTrue">The function to be executed when the underlying specification is satisfied.</param>
    /// <param name="whenFalse">The function to be executed when the underlying specification is not satisfied.</param>
    internal ChangeMetadataTypeSpecification(
        SpecificationBase<TModel, TUnderlyingMetadata> underlyingSpecification,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse)
        : this(
            underlyingSpecification.Description,
            underlyingSpecification,
            whenTrue,
            whenFalse)
    {
    }

    /// <summary>
    /// Gets the description of the specification.
    /// </summary>
    public override string Description => description;

    /// <summary>
    /// Determines if the specification is satisfied by the given model.
    /// </summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>A <see cref="BooleanResultBase{TMetadata}"/> indicating if the specification is satisfied and the resulting metadata.</returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpecification.IsSatisfiedBy(model);
        var metadata = booleanResult.IsSatisfied
            ? whenTrue(model)
            : whenFalse(model);

        return new ChangeMetadataTypeBooleanResult<TMetadata, TUnderlyingMetadata>(booleanResult, metadata);
    }
}