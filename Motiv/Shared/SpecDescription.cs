using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Shared;

public sealed class SpecDescription : ISpecDescription
{
    private readonly string _statement;
    private readonly ISpecDescription? _underlyingDescription;
    private readonly Expression? _expression;

    public SpecDescription(string statement, ISpecDescription? underlyingDescription = null)
    {
        _statement = statement;
        _underlyingDescription = underlyingDescription;
    }

    public SpecDescription(Expression statement, ISpecDescription? underlyingDescription = null)
    {
        _expression = statement;
        _statement = statement.Humanize();
        _underlyingDescription = underlyingDescription;
    }

    public string Statement => _statement;

    public string Detailed => string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        yield return Statement;
        if (_underlyingDescription is null)
            yield break;

        foreach (var line in _underlyingDescription.GetDetailsAsLines())
            yield return line.Indent();
    }

    public string ToReason(bool satisfied) =>
        _expression is null
            ? Statement.ToReason(satisfied)
            : _expression.ToExpressionAssertion(satisfied).Humanize();

    public override string ToString() => Statement;
}
