namespace Karlssberg.Motiv.CompositeFactory;

internal sealed class MultiMetadataCompositeFactoryBooleanResultDescription<TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
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