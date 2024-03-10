namespace Karlssberg.Motiv.ElseIf;

internal sealed class ConsequentBooleanResultDescription<TMetadata>(
    BooleanResultBase<TMetadata> antecedentResult,
    BooleanResultBase<TMetadata> consequentResult)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;
    
    public override string Compact =>
        $"{antecedentResult.Description.Compact} => {consequentResult.Description.Compact}";

    public override string Detailed =>
        $$"""
          {{antecedentResult.Description.Compact}} => {
              {{consequentResult.Description.Detailed.IndentAfterFirstLine()}}
          }
          """;
}