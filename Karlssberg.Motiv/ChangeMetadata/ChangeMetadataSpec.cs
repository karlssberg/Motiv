namespace Karlssberg.Motiv.ChangeMetadata;

internal class ChangeMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    string description,
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse) : SpecBase<TModel, TMetadata>
{
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
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);
        var metadata = booleanResult.IsSatisfied
            ? whenTrue(model)
            : whenFalse(model);

        return new ChangeMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(booleanResult, metadata);
    }
}