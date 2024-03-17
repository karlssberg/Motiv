namespace Karlssberg.Motiv.CompositeFactory;

internal sealed class CompositeFactoryExplanationBooleanResultDescription<TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    string reason)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;
    public override string Compact => reason;
    public override string Detailed =>
        $$"""
          {{Compact}} {
              {{booleanResult.Description.Compact.IndentAfterFirstLine()}}
          }
          """;
}