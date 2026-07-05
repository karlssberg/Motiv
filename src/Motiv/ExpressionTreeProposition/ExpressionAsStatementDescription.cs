using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

internal class ExpressionAsStatementDescription : ISpecDescription
{
    private readonly string _trueBecause;
    private readonly string _falseBecause;
    private readonly ISpecDescription? _underlyingDescription;
    private string? _detailed;

    public ExpressionAsStatementDescription(Expression expression,
        ISpecDescription? underlyingDescription = null)
    {
        _underlyingDescription = underlyingDescription;
        var statement = expression.Serialize();
        _trueBecause = $"{statement} == true";
        _falseBecause = $"{statement} == false";
        Statement = statement;
    }

    public string Statement { get; }

    public string Detailed => _detailed ??= string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        yield return Statement;
        if (_underlyingDescription is null)
            yield break;

        foreach (var line in _underlyingDescription.GetDetailsAsLines())
            yield return line.Indent();
    }

    public string ToReason(bool satisfied) =>
        satisfied
            ? _trueBecause
            : _falseBecause;
}
