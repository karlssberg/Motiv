namespace Karlssberg.Motiv.ChangeMetadataType;

internal sealed class ChangeMetadataBooleanResultDescription<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    TMetadata metadata,
    IProposition proposition)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;
    public override string Compact => proposition.ToReason(booleanResult.Satisfied);

    public override string Detailed =>
        $$"""
          {{Compact}} {
              {{booleanResult.Description.Compact.IndentAfterFirstLine()}}
          }
          """;
}