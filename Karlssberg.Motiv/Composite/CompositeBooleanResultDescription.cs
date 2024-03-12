namespace Karlssberg.Motiv.Composite;

internal sealed class CompositeBooleanResultDescription<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    MetadataTree<TMetadata> metadata,
    IProposition proposition)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;

    public override string Compact =>
        metadata.Count switch
        {
            1 => proposition.ToReason(booleanResult.Satisfied, metadata.Single()),
            _ => proposition.ToReason(booleanResult.Satisfied)
        };

    public override string Detailed =>
        $$"""
          {{Compact}} {
              {{booleanResult.Description.Compact.IndentAfterFirstLine()}}
          }
          """;
}