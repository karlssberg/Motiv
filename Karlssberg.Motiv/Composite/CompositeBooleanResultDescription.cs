namespace Karlssberg.Motiv.Composite;

internal sealed class CompositeBooleanResultDescription<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    TMetadata metadata,
    IProposition proposition)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;
    public override string Compact => proposition.ToReason(booleanResult.Satisfied, metadata);

    public override string Detailed =>
        $$"""
          {{Compact}} {
              {{booleanResult.Description.Compact.IndentAfterFirstLine()}}
          }
          """;
}