using System.Linq.Expressions;

namespace Motiv.ExpressionTrees;

internal interface IExpressionSerializer
{
    internal string Serialize(Expression expression);
}
