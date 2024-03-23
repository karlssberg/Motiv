namespace Karlssberg.Motiv.ElseIf;

internal sealed class ConsequentBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> antecedentResult,
    BooleanResultBase<TMetadata> consequentResult)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;
    
    public override string Reason =>
        $"{antecedentResult.Description.Reason} => {consequentResult.Description.Reason}";

    public override string Detailed =>
        $$"""
          {{antecedentResult.Description.Reason}} => {
              {{consequentResult.Description.Detailed.IndentAfterFirstLine()}}
          }
          """;
}