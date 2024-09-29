using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Motiv.ExpressionTrees;

internal class CSharpExpressionSerializer : ExpressionVisitor, IExpressionSerializer
{
    private readonly StringBuilder _stringBuilder = new();

    public string Serialize(Expression expression)
    {
        _stringBuilder.Clear();
        Visit(expression);
        return _stringBuilder.ToString();
    }

    protected override Expression VisitNew(NewExpression node)
    {
        _stringBuilder.Append("new ");
        _stringBuilder.Append(node.Type.ToCSharpName());
        _stringBuilder.Append("(");

        VisitSpreadOfExpressions(node.Arguments.ToArray());

        _stringBuilder.Append(")");

        return node;
    }
    protected virtual Expression VisitNewInListInit(NewExpression node)
    {
        _stringBuilder.Append("new ");
        _stringBuilder.Append(node.Type.ToCSharpName());
        if (node.Arguments.Count == 0)
        {
            return node;
        }
        _stringBuilder.Append(" (");

        VisitSpreadOfExpressions(node.Arguments.ToArray());

        _stringBuilder.Append(")");

        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (IsIgnoredNode(node))
        {
            Visit(node.Operand);
            return node;
        }
        switch (node.NodeType)
        {
            case ExpressionType.Not:
                _stringBuilder.Append('+');
                _stringBuilder.Append('!');
                Visit(node.Operand);
                break;
            case ExpressionType.Negate:
                _stringBuilder.Append('-');
                Visit(node.Operand);
                break;
            case ExpressionType.UnaryPlus:
                Visit(node.Operand);
                break;
            case ExpressionType.ArrayLength:
                Visit(node.Operand);
                _stringBuilder.Append(".Length");
                break;
            case ExpressionType.ArrayIndex:
                _stringBuilder.Append('[');
                Visit(node.Operand);
                _stringBuilder.Append(']');
                break;
            default:
                _stringBuilder.Append(node.NodeType.ToString());
                break;
        }

        return node;
    }

    protected override Expression VisitBinary(BinaryExpression binaryExpression)
    {
        if (IsIgnoredNode(binaryExpression))
        {
            Visit(binaryExpression.Left);
            return binaryExpression;
        }

        var parenthesizeLeft = NeedsParentheses(binaryExpression, binaryExpression.Left, true);
        var parenthesizeRight = NeedsParentheses(binaryExpression, binaryExpression.Right, false);

        if (parenthesizeLeft) _stringBuilder.Append('(');
        Visit(binaryExpression.Left);
        if (parenthesizeLeft) _stringBuilder.Append(')');

        _stringBuilder.Append(' ');
        _stringBuilder.Append(GetBinaryOperatorSymbol(binaryExpression.NodeType));
        _stringBuilder.Append(' ');

        if (parenthesizeRight) _stringBuilder.Append('(');
        Visit(binaryExpression.Right);
        if (parenthesizeRight) _stringBuilder.Append(')');

        return binaryExpression;
    }

    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
    {
        Visit(node.Expression);
        _stringBuilder.Append(" is ");
        _stringBuilder.Append(node.TypeOperand.ToCSharpName());
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is not ConstantExpression constantExpression)
        {
            Visit(node.Expression);
            _stringBuilder.Append('.');
            _stringBuilder.Append(node.Member.Name);
            return node;
        }

        var (value, valueType) = GetConstantExpressionValue(node.Member.Name, constantExpression);
        if (!IsSupported(value))
        {
            _stringBuilder.Append(node.Member.Name);
            return node;
        }

        var serializeSupported = SerializeSupported(value, valueType);
        _stringBuilder.Append(serializeSupported);

        return node;
    }

    protected override Expression VisitListInit(ListInitExpression node)
    {
        var arguments = node.Initializers.SelectMany(init => init.Arguments).ToArray();
        VisitNewInListInit(node.NewExpression);
        _stringBuilder.Append(" { ");
        VisitSpreadOfExpressions(arguments);
        _stringBuilder.Append(" }");
        return node;
    }

    protected override Expression VisitIndex(IndexExpression node)
    {
        _stringBuilder.Append('[');
        Visit(node.Object);
        _stringBuilder.Append(']');
        return node;
    }

    protected override Expression VisitNewArray(NewArrayExpression node)
    {
        _stringBuilder.Append("new ");
        _stringBuilder.Append(node.Type.GetElementType()!.ToCSharpName());
        if (node.Expressions.Count == 0)
        {
            _stringBuilder.Append("[] {}");
            return node;
        }

        _stringBuilder.Append("[] { ");

        VisitSpreadOfExpressions(node.Expressions);

        _stringBuilder.Append(" }");

        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        var serialization = SerializeSupported(node.Value, node.Type) ?? node.Value.ToString();
        _stringBuilder.Append(serialization);
        return node;
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        var hasSingleParameter = node.Parameters.Count == 1;
        if (!hasSingleParameter)
            _stringBuilder.Append('(');

        VisitSpreadOfExpressions(node.Parameters);

        if (!hasSingleParameter)
            _stringBuilder.Append(')');

        _stringBuilder.Append(" => ");

        Visit(node.Body);

        return node;
    }

    protected Expression VisitObjectIndex(MethodCallExpression node)
    {
        Visit(node.Object);
        _stringBuilder.Append('[');
        VisitSpreadOfExpressions(node.Arguments);
        _stringBuilder.Append(']');
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.IsSpecialName && node.Method.Name == "get_Item")
            return VisitObjectIndex(node);

        var isExtensionMethod = node.Method.IsDefined(typeof(ExtensionAttribute), false);
        var instanceObject = isExtensionMethod ? node.Arguments[0] : node.Object;
        var arguments = isExtensionMethod ? node.Arguments.Skip(1) : node.Arguments;

        Visit(instanceObject);
        _stringBuilder.Append('.');
        _stringBuilder.Append(node.Method.ToCSharpName());
        _stringBuilder.Append("(");
        VisitSpreadOfExpressions(arguments.ToArray());
        _stringBuilder.Append(")");

        return node;
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        var needsParentheses = NeedsParentheses(node, node.Expression, true);
        if (needsParentheses)
            _stringBuilder.Append('(');
        Visit(node.Expression);
        if (needsParentheses)
            _stringBuilder.Append(')');

        _stringBuilder.Append('(');
        VisitSpreadOfExpressions(node.Arguments);
        _stringBuilder.Append(')');
        return node;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        _stringBuilder.Append(node.Name);
        return node;
    }

    private void VisitSpreadOfExpressions(IEnumerable<Expression> arguments)
    {
        var isFirst = true;
        foreach (var arg in arguments)
        {
            if (isFirst)
            {
                isFirst = false;
                Visit(arg);
                continue;
            }

            _stringBuilder.Append(", ");
            Visit(arg);
        }
    }

    private static (object?, Type) GetConstantExpressionValue(string capturedVariableName,
        ConstantExpression constantExpression)
    {
        var value = constantExpression.Value;
        var valueType = constantExpression.Type;

        if (value == null || !IsClosureObject(value.GetType())) return (value, valueType); // not a closure

        var capturedField = GetCapturedField(capturedVariableName, value);

        return capturedField != null
            ? (capturedField.GetValue(value), capturedField.FieldType)
            : (value, valueType); // If no suitable field found, return the closure object itself
    }

    private static FieldInfo? GetCapturedField(string capturedVariableName, object value) =>
        value.GetType().GetField(capturedVariableName);

    protected static bool IsClosureObject(Type type) =>
        // Closure types are compiler-generated and usually have specific naming patterns
        type.Name.StartsWith("<>") || type.Name.Contains("DisplayClass");

    private static string GetBinaryOperatorSymbol(ExpressionType nodeType) =>
        nodeType switch
        {
            ExpressionType.And => "&",
            ExpressionType.AndAlso => "&&",
            ExpressionType.Or => "|",
            ExpressionType.OrElse => "||",
            ExpressionType.Equal => "==",
            ExpressionType.NotEqual => "!=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.ExclusiveOr => "^",
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            ExpressionType.Modulo => "%",
            ExpressionType.LeftShift => "<<",
            ExpressionType.RightShift => ">>",
            ExpressionType.Not => "!",
            ExpressionType.Negate => "-",
            ExpressionType.Quote => "\"",
            ExpressionType.Coalesce => "??",
            _ => nodeType.ToString()
        };

    private static bool NeedsParentheses(Expression parent, Expression child, bool isLeft)
    {
        child = ResolveVisibleNode(child);
        if (child is not BinaryExpression and not LambdaExpression)
            return false;

        var parentPrecedence = GetOperatorPrecedence(parent.NodeType);
        var childPrecedence = GetOperatorPrecedence(child.NodeType);

        if (parentPrecedence > childPrecedence)
            return true;

        if (parentPrecedence != childPrecedence)
            return false;

        if (parent.NodeType != child.NodeType)
            return true;

        // Handle right-associative operators (e.g., assignment)
        if (IsRightAssociative(parent.NodeType) && !isLeft)
            return true;

        // For left-associative operators, parenthesize the right operand
        return !isLeft;
    }

    private static int GetOperatorPrecedence(ExpressionType nodeType)
    {
        return nodeType switch
        {
            // Primary expressions (the highest precedence)
            ExpressionType.Call
                or ExpressionType.MemberAccess
                or ExpressionType.Index => 15,
            // Unary operators
            ExpressionType.Not
                or ExpressionType.Negate
                or ExpressionType.UnaryPlus
                or ExpressionType.Convert => 14,
            // Multiplicative operators
            ExpressionType.Multiply
                or ExpressionType.Divide
                or ExpressionType.Modulo => 13,
            // Additive operators
            ExpressionType.Add
                or ExpressionType.Subtract => 12,
            // Shift operators
            ExpressionType.LeftShift
                or ExpressionType.RightShift => 11,
            // Relational and type testing operators
            ExpressionType.LessThan
                or ExpressionType.LessThanOrEqual
                or ExpressionType.GreaterThan
                or ExpressionType.GreaterThanOrEqual
                or ExpressionType.TypeIs
                or ExpressionType.TypeAs => 10,
            // Equality operators
            ExpressionType.Equal
                or ExpressionType.NotEqual => 9,
            // Logical AND
            ExpressionType.And => 8,
            // Logical XOR
            ExpressionType.ExclusiveOr => 7,
            // Logical OR
            ExpressionType.Or => 6,
            // Conditional AND
            ExpressionType.AndAlso => 5,
            // Conditional OR
            ExpressionType.OrElse => 4,
            // Null-coalescing operator
            ExpressionType.Coalesce => 3,
            // Conditional operator (ternary)
            ExpressionType.Conditional => 2,
            // Assignment operators (lowest precedence)
            ExpressionType.Assign
                or ExpressionType.AddAssign
                or ExpressionType.SubtractAssign
                or ExpressionType.MultiplyAssign
                or ExpressionType.DivideAssign
                or ExpressionType.ModuloAssign
                or ExpressionType.AndAssign
                or ExpressionType.OrAssign
                or ExpressionType.ExclusiveOrAssign
                or ExpressionType.LeftShiftAssign
                or ExpressionType.RightShiftAssign => 1,
            _ => 0
        };
    }

    private static bool IsRightAssociative(ExpressionType nodeType)
    {
        return nodeType switch
        {
            // Assignment operators
            ExpressionType.Assign
                or ExpressionType.AddAssign
                or ExpressionType.SubtractAssign
                or ExpressionType.MultiplyAssign
                or ExpressionType.DivideAssign
                or ExpressionType.ModuloAssign
                or ExpressionType.AndAssign
                or ExpressionType.OrAssign
                or ExpressionType.ExclusiveOrAssign
                or ExpressionType.LeftShiftAssign
                or ExpressionType.RightShiftAssign
                or ExpressionType.PowerAssign => true,
            // Null-coalescing operator
            ExpressionType.Coalesce => true,
            // Lambda expressions (considered right-associative in many contexts)
            ExpressionType.Lambda => true,
            _ => false
        };
    }

    private static bool IsSupported<T>(T value) =>
        value is null
            or bool
            or char
            or string
            or float
            or double
            or decimal
            or long
            or ulong
            or int
            or byte
            or sbyte
            or short
            or ushort
            or uint
            or ulong
            or Guid
            or TimeSpan
            or DateTime
            or DateTimeOffset
            or Regex
            or Type;

    private static string? SerializeSupported(object? obj, Type declaredType)
    {
        return obj switch
        {
            null => $"default({declaredType.ToCSharpName()})",
            bool b => b ? "true" : "false",
            char ch => $"'{ch}'",
            string s => $"\"{s}\"",
            float f => $"{f}f",
            double d => $"{d}d",
            decimal d => $"{d}m",
            long l => $"{l}L",
            ulong ul => $"{ul}UL",
            Guid guid => $"Guid.Parse({guid})",
            DateTime dateTime => $"DateTime.Parse(\"{dateTime:O}\")",
            DateTimeOffset dateTimeOffset => $"DateTimeOffset.Parse(\"{dateTimeOffset:O}\")",
            TimeSpan timespan => $"TimeSpan.Parse(\"{timespan}\")",
            Regex regex => $"new Regex(@\"{regex}\")",
            Type t => $"typeof({t.FullName})",
            _ when IsSupported(obj) => obj.ToString(),
            _ => null
        };
    }

    private static bool IsIgnoredNode(Expression node) =>
        node.NodeType switch
        {
            ExpressionType.Quote => true,
            ExpressionType.IsTrue => true,
            ExpressionType.IsFalse => true,
            ExpressionType.ConvertChecked => true,
            ExpressionType.Convert => true,
            _ => false
        };

    private static Expression ResolveVisibleNode(Expression node) =>
        node switch
        {
            // Ignored nodes
            UnaryExpression { NodeType: ExpressionType.Quote } unary => unary.Operand,
            UnaryExpression { NodeType: ExpressionType.TypeAs } unary => unary.Operand,
            UnaryExpression { NodeType: ExpressionType.Coalesce } unary => unary.Operand,
            UnaryExpression { NodeType: ExpressionType.IsTrue } unary => unary.Operand,
            UnaryExpression { NodeType: ExpressionType.IsFalse } unary => unary.Operand,
            UnaryExpression { NodeType: ExpressionType.ArrayLength } unary => unary.Operand,
            UnaryExpression { NodeType: ExpressionType.ConvertChecked } unary => unary.Operand,
            UnaryExpression { NodeType: ExpressionType.Convert } unary => unary.Operand,
            _ => node
        };
}
