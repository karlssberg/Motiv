namespace Karlssberg.Motiv.ChangeMetadataType;

internal class ChangeMetadataBooleanResultDescription<TMetadata, TUnderlyingMetadata> (
    BooleanResultBase<TUnderlyingMetadata> booleanResult, 
    TMetadata metadata,
    IProposition proposition)
    : IResultDescription
{
    public int CausalOperandCount => 1;
    public string Reason => proposition.ToReason(booleanResult.Satisfied, metadata);
    public string Details =>
       $"""
        {Reason}
            {booleanResult.Description.Details.IndentAfterFirstLine()}
        """;
    
    public override string ToString() => Reason;
}