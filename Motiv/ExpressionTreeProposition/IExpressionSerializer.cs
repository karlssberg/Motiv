using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal interface IExpressionSerializer
{
    internal string Serialize(Expression expression);
}
