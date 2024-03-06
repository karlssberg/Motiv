﻿namespace Karlssberg.Motiv.HigherOrder;

/// <summary>Represents a boolean result with an associated model and metadata.</summary>
/// <typeparam name="TModel">The type of the associated model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public sealed class BooleanResultWithModel<TModel, TMetadata> : BooleanResultBase<TMetadata>
{
    /// <summary>Gets the underlying boolean result.</summary>
    private readonly BooleanResultBase<TMetadata> _underlyingResult;

    /// <summary>Initializes a new instance of the BooleanResultWithModel class.</summary>
    /// <param name="model">The associated model.</param>
    /// <param name="underlyingResult">The underlying boolean result.</param>
    internal BooleanResultWithModel(
        TModel model,
        BooleanResultBase<TMetadata> underlyingResult)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _underlyingResult = underlyingResult ?? throw new ArgumentNullException(nameof(underlyingResult));
    }

    /// <summary>Gets the associated model.</summary>
    public TModel Model { get; }

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description => _underlyingResult.Description;

    public override Explanation Explanation => _underlyingResult.Explanation;

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied => _underlyingResult.Satisfied;

    public override MetadataSet<TMetadata> Metadata => _underlyingResult.Metadata;
}