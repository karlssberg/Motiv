using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Shared;

internal sealed class ExpressionDescription(Expression statement, ISpecDescription? underlyingDescription = null) : ISpecDescription
{
    private string? _reasonWhenTrue;
    private string? _reasonWhenFalse;
    private string? _detailed;

    public string Statement { get; } = statement.Serialize();

    public string Detailed => _detailed ??= string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        yield return Statement;
        if (underlyingDescription is null)
            yield break;

        foreach (var line in underlyingDescription.GetDetailsAsLines())
            yield return line.Indent();
    }

    public string ToReason(bool satisfied) => satisfied
        ? _reasonWhenTrue ??= statement.ToExpressionAssertion(true).Serialize()
        : _reasonWhenFalse ??= statement.ToExpressionAssertion(false).Serialize();

    public bool HasExplicitStatement { get; set; }

    public override string ToString() => Statement;
}
