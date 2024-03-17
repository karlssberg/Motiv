namespace Karlssberg.Motiv.Composite;

internal sealed class CompositeExplanationBooleanResultDescription<TUnderlyingMetadata>(
    string because,
    BooleanResultBase<TUnderlyingMetadata> booleanResult)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;

    public override string Compact => because;

    public override string Detailed =>
        $$"""
          {{Compact}} {
              {{booleanResult.Description.Compact.IndentAfterFirstLine()}}
          }
          """;
}