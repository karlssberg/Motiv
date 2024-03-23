namespace Karlssberg.Motiv;

internal sealed class BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    string reason)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;

    public override string Reason => reason;
    public override string Detailed =>
        $$"""
          {{Reason}} {
              {{booleanResult.Description.Reason.IndentAfterFirstLine()}}
          }
          """;
}