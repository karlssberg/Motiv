using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

/// <summary>
/// Finds whether an expression references a parameter it does not declare itself — i.e. whether its value depends
/// on the input of an enclosing lambda. Parameters bound by a lambda nested inside the expression do not count.
/// </summary>
internal sealed class FreeParameterDetector : ExpressionVisitor
{
    private readonly HashSet<ParameterExpression> _declared = [];
    private bool _found;

    internal static bool HasFreeParameter(Expression expression)
    {
        var detector = new FreeParameterDetector();
        detector.Visit(expression);
        return detector._found;
    }

    public override Expression? Visit(Expression? node) => _found ? node : base.Visit(node);

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        _declared.UnionWith(node.Parameters);
        return base.VisitLambda(node);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        _found |= !_declared.Contains(node);
        return node;
    }
}
