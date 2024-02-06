namespace Karlssberg.Motiv.All;

/// <summary>Represents a specification that checks if all elements in a collection satisfy the underlying specification.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata.</typeparam>
internal sealed class AllSpec<TModel, TMetadata> : SpecBase<IEnumerable<TModel>, TMetadata>
{
    // Optional description of the specification.
    private readonly string? _description;

    /// <summary>
    /// Initializes a new instance of the AllSatisfiedSpec class with an underlying specification, a metadata factory,
    /// and an optional description.
    /// </summary>
    /// <param name="propositionalSpec"></param>
    /// <param name="description">The optional description.</param>
    internal AllSpec(
        SpecBase<TModel, TMetadata> propositionalSpec,
        string? description = null)
    {
        UnderlyingSpec = propositionalSpec;
        _description = description;
    }

    /// <summary>Gets the description of the specification.</summary>
    public override string Description => _description switch
    {
        null =>  $"ALL({UnderlyingSpec})",
        not null => $"<{_description}>({UnderlyingSpec})"
    };

    /// <summary>Gets the underlying specification.</summary>
    public SpecBase<TModel, TMetadata> UnderlyingSpec { get; }

    /// <summary>Determines if all elements in the collection satisfy the underlying specification.</summary>
    /// <param name="models">The collection to be evaluated.</param>
    /// <returns>A BooleanResultBase object containing the result of the evaluation.</returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model =>
            {
                var underlyingResult = UnderlyingSpec.IsSatisfiedByOrWrapException(model);
                return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
            })
            .ToArray();

        var isSatisfied = underlyingResults.All(result => result.Value);
        return new AllBooleanResult<TModel, TMetadata>(
            isSatisfied,
            underlyingResults);
    }
}