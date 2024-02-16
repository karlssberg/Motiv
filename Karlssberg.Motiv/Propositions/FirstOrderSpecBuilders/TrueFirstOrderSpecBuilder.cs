using Karlssberg.Motiv.Propositions.FirstOrderSpecBuilders.Metadata;
using Karlssberg.Motiv.Propositions.FirstOrderSpecBuilders.Reasons;

namespace Karlssberg.Motiv.Propositions.FirstOrderSpecBuilders;

public readonly struct TrueFirstOrderSpecBuilder<TModel>(Func<TModel, bool> predicate)
{
    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseReasonFirstOrderSpecBuilder{TModel}" />.</returns>
    public FalseReasonFirstOrderSpecBuilder<TModel> WhenTrue(string trueBecause)
    {
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        return new FalseReasonFirstOrderSpecBuilder<TModel>(
            predicate, 
            _ => trueBecause,
            trueBecause);
    }

    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseReasonFirstOrderSpecBuilder{TModel}" />.</returns>
    public FalseReasonWithDescriptionUnresolvedFirstOrderSpecBuilder<TModel> WhenTrue(Func<TModel, string> trueBecause)
    {
        trueBecause.ThrowIfNull(nameof(trueBecause));
        return new FalseReasonWithDescriptionUnresolvedFirstOrderSpecBuilder<TModel>(predicate, trueBecause);
    }

    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataFirstOrderSpecBuilder{TModel,TMetadata}" />.</returns>
    public FalseMetadataFirstOrderSpecBuilder<TModel, TMetadata> WhenTrue<TMetadata>(TMetadata whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataFirstOrderSpecBuilder<TModel, TMetadata>(predicate, _ => whenTrue);
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
}