namespace Karlssberg.Motiv.Any;

/// <summary>
/// Represents a specification that is satisfied if any model in a collection satisfies an underlying specification.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal class AnySatisfiedSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> underlyingSpec,
    string? description = null) : SpecBase<IEnumerable<TModel>, TMetadata>
{

    /// <summary>
    /// Gets the description of the specification.
    /// </summary>
    public override string Description => description switch
    {
        null => $"ANY({UnderlyingSpec})",
        _ => $"<{description}>({UnderlyingSpec})"
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
        var resultsWithModel = models
            .Select(model =>
            {
                var underlyingResult = UnderlyingSpec.IsSatisfiedByOrWrapException(model);
                return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
            })
            .ToArray();

        var isSatisfied = resultsWithModel.Any(result => result.IsSatisfied);
        return new AnySatisfiedBooleanResult<TModel, TMetadata>(
            isSatisfied,
            resultsWithModel);
    }
}