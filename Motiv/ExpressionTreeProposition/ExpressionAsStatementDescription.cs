using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

internal class ExpressionAsStatementDescription<TModel> : IExpressionDescription<TModel>
{
    private readonly string _trueBecause;
    private readonly string _falseBecause;
    private readonly Expression _expression;
    private readonly ParameterExpression _parameter;
    private readonly ISpecDescription? _underlyingDescription;

    public ExpressionAsStatementDescription(Expression expression,
        ParameterExpression parameter,
        ISpecDescription? underlyingDescription = null)
    {
        _expression = expression;
        _parameter = parameter;
        _underlyingDescription = underlyingDescription;
        var statement = expression.Humanize();
        _trueBecause = $"{statement} == true";
        _falseBecause = $"{statement} == false";
        Statement = statement;
    }

    public string Statement { get; }

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
        satisfied
            ? _trueBecause
            : _falseBecause;


    public string ToAssertion(TModel model, bool satisfied) =>
        _expression.ToExpressionAssertion(satisfied).Humanize(model, _parameter);
}
