namespace Karlssberg.Motiv.ElseIf;

internal sealed class ElseIfProposition<TModel>(
    SpecBase<TModel> antecedent,
    SpecBase<TModel> consequent)
    : IProposition
{
    public string Statement => $"{antecedent.Proposition.Statement} => {consequent.Proposition.Statement}";

    public string Detailed =>
        $$"""
          {{antecedent.Proposition.Detailed}} => {
              {{consequent.Proposition.Detailed.IndentAfterFirstLine()}}
          }
          """;

    public override string ToString() => Statement;
}