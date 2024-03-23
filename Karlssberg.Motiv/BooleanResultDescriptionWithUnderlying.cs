namespace Karlssberg.Motiv;

internal sealed class BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    string because)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;

    public override string Reason => because;
    public override string Detailed =>
        $$"""
          {{Reason}} {
              {{booleanResult.Description.Reason.IndentAfterFirstLine()}}
          }
          """;
}