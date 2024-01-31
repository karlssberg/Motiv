namespace Karlssberg.Motiv.Any;

/// <summary>
/// Represents a specification that is satisfied if any model in a collection satisfies an underlying specification.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class AnySatisfiedSpec<TModel, TMetadata> : SpecBase<IEnumerable<TModel>, TMetadata>
{
    private readonly string? _description;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnySatisfiedSpec{TModel, TMetadata}"/> class.
    /// </summary>
    /// <param name="propositionalSpec">The underlying specification.</param>
    /// <param name="description">The description of the specification.</param>
    internal AnySatisfiedSpec(
        SpecBase<TModel, TMetadata> propositionalSpec,
        string? description = null)
    {
        UnderlyingSpec = propositionalSpec;
        _description = description;
    }

    /// <summary>
    /// Gets the description of the specification.
    /// </summary>
    public override string Description => _description is null
        ? $"ANY({UnderlyingSpec})"
        : $"ANY<{_description}>({UnderlyingSpec})";

    /// <summary>
    /// Gets the underlying specification.
    /// </summary>
    public SpecBase<TModel, TMetadata> UnderlyingSpec { get; }

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