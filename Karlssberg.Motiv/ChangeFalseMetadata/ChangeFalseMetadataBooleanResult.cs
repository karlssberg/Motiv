using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.ChangeFalseMetadata;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The underlying metadata type that is being changed.</typeparam>
internal class ChangeFalseMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult, 
    TMetadata metadata) 
    : BooleanResultBase<TMetadata>, IChangeMetadataBooleanResult<TMetadata>
{
    /// <summary>Gets the underlying boolean result.</summary>
    public BooleanResultBase<TUnderlyingMetadata> UnderlyingBooleanResult => booleanResult;

    /// <summary>Gets the new metadata.</summary>
    public IEnumerable<TMetadata> Metadata => [metadata];

    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Satisfied => UnderlyingBooleanResult.Satisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override string Description =>
        metadata switch
        {
            string reason => reason,
            _ => UnderlyingBooleanResult.Description
        };

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults => 
        booleanResult switch
        {
            BooleanResultBase<TMetadata> result => [result],
            _ => []
        };

    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands =>
        booleanResult switch
        {
            BooleanResultBase<TMetadata> result => result.DeterminativeOperands,
            _ => []
        };

    /// <summary>Gets the type of the original metadata.</summary>
    public Type OriginalMetadataType => typeof(TUnderlyingMetadata);

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override IEnumerable<Reason> ReasonHierarchy =>
        metadata switch
        {
            string reason => [new Reason(reason, UnderlyingBooleanResult.ReasonHierarchy)],
            _ => UnderlyingBooleanResult.ReasonHierarchy
        };
}