namespace Karlssberg.Motiv;

internal sealed class BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    string because)
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