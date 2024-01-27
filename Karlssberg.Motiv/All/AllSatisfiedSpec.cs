﻿using Karlssberg.Motiv.NSatisfied;

namespace Karlssberg.Motiv.All;

/// <summary>Represents a specification that checks if all elements in a collection satisfy the underlying specification.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public class AllSatisfiedSpec<TModel, TMetadata, TUnderlyingMetadata> :
    SpecBase<IEnumerable<TModel>, TMetadata>,
    IHaveUnderlyingSpec<TModel, TUnderlyingMetadata>
{
    // Optional description of the specification.
    private readonly string? _description;

    // Function to generate metadata based on the result of the specification.
    private readonly
        MetadataFactory<TModel, TMetadata, TUnderlyingMetadata>
        _metadataFactory;

    /// <summary>
    /// Initializes a new instance of the AllSatisfiedSpec class with an underlying specification, a metadata factory,
    /// and an optional description.
    /// </summary>
    /// <param name="underlyingSpec">The underlying specification.</param>
    /// <param name="metadataFactory">The metadata factory.</param>
    /// <param name="description">The optional description.</param>
    internal AllSatisfiedSpec(
        SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
        MetadataFactory<TModel, TMetadata, TUnderlyingMetadata> metadataFactory,
        string? description = null)
    {
        UnderlyingSpec = underlyingSpec;
        _metadataFactory = metadataFactory;
        _description = description;
    }

    /// <summary>Gets the description of the specification.</summary>
    public override string Description => _description is null
        ? $"ALL({UnderlyingSpec})"
        : $"ALL<{_description}>({UnderlyingSpec})";

    /// <summary>Gets the underlying specification.</summary>
    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; }

    /// <summary>Determines if all elements in the collection satisfy the underlying specification.</summary>
    /// <param name="models">The collection to be evaluated.</param>
    /// <returns>A BooleanResultBase object containing the result of the evaluation.</returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var resultsWithModel = models
            .Select(model =>
            {
                var underlyingResult = UnderlyingSpec.IsSatisfiedByOrWrapException(model);
                return new BooleanResultWithModel<TModel, TUnderlyingMetadata>(model, underlyingResult);
            })
            .ToArray();

        var isSatisfied = resultsWithModel.All(result => result.IsSatisfied);
        return new AllSatisfiedBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            _metadataFactory.Create(isSatisfied, resultsWithModel),
            resultsWithModel);
    }
}

/// <summary>Represents a specification that checks if all elements in a collection satisfy the underlying specification.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class AllSatisfiedSpec<TModel, TMetadata> : AllSatisfiedSpec<TModel, TMetadata, TMetadata>
{
    /// <summary>
    /// Initializes a new instance of the AllSatisfiedSpec class with an underlying specification and a metadata
    /// factory.
    /// </summary>
    /// <param name="underlyingSpec">The underlying specification.</param>
    /// <param name="metadataFactory">The metadata factory.</param>
    internal AllSatisfiedSpec(
        SpecBase<TModel, TMetadata> underlyingSpec,
        MetadataFactory<TModel, TMetadata, TMetadata> metadataFactory)
        : base(underlyingSpec, metadataFactory)
    {
    }
}