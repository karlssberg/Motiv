namespace Karlssberg.Motiv.Composite;

internal sealed class CompositeMetadataBooleanResultDescription<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    string because,
    IProposition proposition)
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