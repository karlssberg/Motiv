﻿namespace Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Metadata;

/// <summary>
/// Represents a builder for creating specifications based on a predicate and metadata factories. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue)
{
    /// <summary>
    /// Specifies the metadata to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">The metadata to use when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataCompositeFactorySpecFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataCompositeFactorySpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        TMetadata whenFalse) =>
        new(specPredicate,
            whenTrue,
            (_, _) => whenFalse.ToEnumerable());

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataCompositeFactorySpecFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataCompositeFactorySpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<TModel, TMetadata> whenFalse) =>
        new(specPredicate,
            whenTrue,
            (model, _) => whenFalse(model).ToEnumerable());

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataCompositeFactorySpecFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataCompositeFactorySpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse) =>
        new(specPredicate,
            whenTrue,
            (model, result) => whenFalse(model, result).ToEnumerable());

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MetadataCompositeFactorySpecFactory{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public MetadataCompositeFactorySpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse) =>
        new(specPredicate,
            whenTrue,
            whenFalse);
}