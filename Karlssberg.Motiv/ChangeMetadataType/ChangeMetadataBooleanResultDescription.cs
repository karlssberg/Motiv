namespace Karlssberg.Motiv.ChangeMetadataType;

internal class ChangeMetadataBooleanResultDescription<TMetadata, TUnderlyingMetadata> (
    BooleanResultBase<TUnderlyingMetadata> booleanResult, 
    TMetadata metadata,
    IProposition proposition)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;
    public override string Compact => proposition.ToReason(booleanResult.Satisfied, metadata);
    public override string Detailed =>
       $"""
        {Compact}
            {booleanResult.Description.Detailed.IndentAfterFirstLine()}
        """;
}