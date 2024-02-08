namespace Karlssberg.Motiv.ChangeMetadataType;

/// <summary>Represents the result of changing the metadata type to a boolean value.</summary>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public interface IChangeMetadataBooleanResult<TMetadata>
{
    /// <summary>Gets the metadata after the type change.</summary>
    IEnumerable<TMetadata> Metadata { get; }

    /// <summary>Gets a value indicating whether the change was successful.</summary>
    bool Value { get; }

    /// <summary>Gets a description of the result.</summary>
    string Description { get; }

    IEnumerable<string> Reasons { get; }

    /// <summary>Gets the original type of the metadata.</summary>
    Type OriginalMetadataType { get; }

    IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }
    
    IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands { get; }
}