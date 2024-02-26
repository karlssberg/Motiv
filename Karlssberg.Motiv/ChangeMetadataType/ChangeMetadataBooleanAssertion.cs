namespace Karlssberg.Motiv.ChangeMetadataType;

internal class ChangeMetadataBooleanAssertion<TMetadata, TUnderlyingMetadata> (
    BooleanResultBase<TUnderlyingMetadata> booleanResult, 
    TMetadata metadata,
    IProposition proposition)
    : IAssertion
{
    public int CausalOperandCount => 1;
    public string Short => proposition.ToReason(booleanResult.Satisfied, metadata);
    public string Detailed =>
       $"""
        {Short}
            {booleanResult.Assertion.Detailed.IndentAfterFirstLine()}
        """;
    
    public override string ToString() => Short;
}