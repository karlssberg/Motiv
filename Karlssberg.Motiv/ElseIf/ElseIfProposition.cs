namespace Karlssberg.Motiv.ElseIf;

internal sealed class ElseIfProposition<TModel>(
    SpecBase<TModel> antecedent,
    SpecBase<TModel> consequent)
    : IProposition
{
    public string Assertion => $"{antecedent.Proposition.Assertion} => {consequent.Proposition.Assertion}";

    public string Detailed =>
        $$"""
          {{antecedent.Proposition.Detailed}} => {
              {{consequent.Proposition.Detailed.IndentAfterFirstLine()}}
          }
          """;

    public override string ToString() => Assertion;
}