using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Shared;

internal sealed class ExpressionDescription(Expression statement, ISpecDescription? underlyingDescription = null) : ISpecDescription
{

    public string Statement => statement.Humanize();

    public string Detailed => string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        yield return Statement;
        if (underlyingDescription is null)
            yield break;

        foreach (var line in underlyingDescription.GetDetailsAsLines())
            yield return line.Indent();
    }

    public string ToReason(bool satisfied) =>
        statement.ToExpressionAssertion(satisfied).Humanize();

    public override string ToString() => Statement;
}
