namespace Karlssberg.Motiv.ChangeMetadata;

/// <summary>Represents the result of changing the metadata type to a boolean value.</summary>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public interface IChangeMetadataBooleanResult<out TMetadata>
{
    /// <summary>Gets the metadata after the type change.</summary>
    TMetadata Metadata { get; }

    /// <summary>Gets a value indicating whether the change was successful.</summary>
    bool IsSatisfied { get; }

    /// <summary>Gets a description of the result.</summary>
    string Description { get; }

    /// <summary>Gets the original type of the metadata.</summary>
    Type OriginalMetadataType { get; }
}