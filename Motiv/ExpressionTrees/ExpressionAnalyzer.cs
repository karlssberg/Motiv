using System.Linq.Expressions;

namespace Motiv.ExpressionTrees;

internal class ExpressionAnalyzer : ExpressionVisitor
{
    private List<Expression> _asValueArguments = [];

    public IEnumerable<Expression> FindAsValueArguments(Expression expression)
    {
        _asValueArguments = [];
        Visit(expression);
        return _asValueArguments;
    }
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == nameof(Display.AsValue) && node.Method.DeclaringType == typeof(Display))
            _asValueArguments.Add(node.Arguments[0]);
        return node;
    }
}
