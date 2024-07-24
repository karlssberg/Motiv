﻿namespace Motiv.SpecDecoratorProposition.PropositionBuilders.Metadata;

/// <summary>
/// A builder for creating propositions based on an existing proposition and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly ref struct FalseMetadataPolicyBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    PolicyBase<TModel, TUnderlyingMetadata> policy,
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>, TMetadata> whenTrue)
{
    /// <summary>
    /// Specifies the metadata to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">The metadata to use when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and metadata factories.</returns>
    public MetadataPolicyFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        TMetadata whenFalse) =>
        new(policy,
            whenTrue,
            (_, _) => whenFalse);

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates the metadata when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and metadata factories.</returns>
    public MetadataPolicyFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<TModel, TMetadata> whenFalse) =>
        new(policy,
            whenTrue,
            (model, _) => whenFalse(model));

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates the metadata when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and metadata factories.</returns>
    public MetadataPolicyFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<TModel, PolicyResultBase<TUnderlyingMetadata>, TMetadata> whenFalse) =>
        new(policy,
            whenTrue,
            whenFalse);

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is false.
    /// </summary>
    /// <param name="whenFalse">A function that generates a collection of metadata when the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied proposition and metadata factories.</returns>
    public MultiMetadataFromPolicyPropositionFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalseYield(
        Func<TModel, PolicyResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse) =>
        new(policy,
            whenTrue.ToEnumerableReturn(),
            whenFalse);
}