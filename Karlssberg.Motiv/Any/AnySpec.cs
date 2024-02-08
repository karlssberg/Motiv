namespace Karlssberg.Motiv.Any;

/// <summary>
/// Represents a specification that is satisfied if any model in a collection satisfies an underlying specification.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal sealed class AnySpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> underlyingSpec,
    string? description = null)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{

    /// <summary>
    /// Gets the description of the specification.
    /// </summary>
    public override string Description => 
        description switch
        {
            null => $"ANY({UnderlyingSpec})",
            not null => $"<{description}>({UnderlyingSpec})"
        };

    /// <summary>
    /// Gets the underlying specification.
    /// </summary>
    public SpecBase<TModel, TMetadata> UnderlyingSpec { get; } = underlyingSpec;

    /// <summary>
    /// Determines whether the specification is satisfied by a collection of models.
    /// </summary>
    /// <param name="models">The collection of models.</param>
    /// <returns>A boolean result with metadata.</returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(UnderlyingSpec.IsSatisfiedByOrWrapException)
            .ToArray();

        return new AnyBooleanResult<TModel, TMetadata>(underlyingResults);
    }
}