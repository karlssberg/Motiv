﻿using Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Explanation;
using Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Metadata;

namespace Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders;

public readonly ref struct TrueFirstOrderSpecBuilder<TModel>(Func<TModel, bool> predicate)
{
    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionFirstOrderSpecBuilder{TModel}" />.</returns>
    public FalseAssertionFirstOrderSpecBuilder<TModel> WhenTrue(string trueBecause) =>
        new(predicate, 
            _ => trueBecause,
            trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause)));

    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionFirstOrderSpecBuilder{TModel}" />.</returns>
    public FalseAssertionWithPropositionUnresolvedFirstOrderSpecBuilder<TModel> WhenTrue(Func<TModel, string> trueBecause) =>
        new(predicate,
            trueBecause.ThrowIfNull(nameof(trueBecause)));

    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataFirstOrderSpecBuilder{TModel,TMetadata}" />.</returns>
    public FalseMetadataFirstOrderSpecBuilder<TModel, TMetadata> WhenTrue<TMetadata>(TMetadata whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataFirstOrderSpecBuilder<TModel, TMetadata>(
            predicate, 
            _ => whenTrue);
    }

    /// <summary>Specifies the behavior when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The function that defines the behavior when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataFirstOrderSpecBuilder{TModel,TMetadata}" />.</returns>
    public FalseMetadataFirstOrderSpecBuilder<TModel, TMetadata> WhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataFirstOrderSpecBuilder<TModel, TMetadata>(predicate, whenTrue);
    }
    
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        new ExplanationSpec<TModel>(
            predicate, 
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}