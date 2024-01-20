namespace Karlssberg.Motiv.ChangeMetadata;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TOtherMetadata">The type of the original metadata.</typeparam>
internal class ChangeMetadataBooleanResult<TMetadata, TOtherMetadata>(BooleanResultBase<TOtherMetadata> booleanResult, TMetadata metadata)
    : BooleanResultBase<TMetadata>, IChangeMetadataBooleanResult<TMetadata>
{
    /// <summary>Gets the underlying boolean result.</summary>
    public BooleanResultBase<TOtherMetadata> UnderlyingBooleanResult => booleanResult;

    /// <summary>Gets the new metadata.</summary>
    public TMetadata Metadata => metadata;

    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool IsSatisfied => UnderlyingBooleanResult.IsSatisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override string Description => metadata switch
    {
        string reason => reason,
        _ => UnderlyingBooleanResult.Description
    };

    /// <summary>Gets the type of the original metadata.</summary>
    public Type OriginalMetadataType => typeof(TOtherMetadata);

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override IEnumerable<string> GatherReasons() =>
        metadata switch
        {
            string reason => [reason],
            _ => UnderlyingBooleanResult.GatherReasons()
        };
}