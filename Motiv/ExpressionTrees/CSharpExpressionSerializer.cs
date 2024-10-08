using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Motiv.ExpressionTrees;


internal class CSharpExpressionSerializer : ExpressionVisitor, IExpressionSerializer
{
    protected readonly StringBuilder OutputText = new();

    public string Serialize(Expression expression)
    {
        OutputText.Clear();
        Visit(expression);
        return OutputText.ToString();
    }

    protected override Expression VisitNew(NewExpression node)
    {
        OutputText.Append("new ");
        OutputText.Append(node.Type.ToCSharpName());
        OutputText.Append('(');

        VisitSpreadOfExpressions(node.Arguments.ToArray());

        OutputText.Append(')');

        return node;
    }

    protected virtual Expression VisitNewWithInitialization(NewExpression node)
    {
        OutputText.Append("new ");
        OutputText.Append(node.Type.ToCSharpName());
        if (node.Arguments.Count == 0)
        {
            return node;
        }
        OutputText.Append(" (");

        VisitSpreadOfExpressions(node.Arguments.ToArray());

        OutputText.Append(")");

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
            case ExpressionType.ArrayLength:
                VisitAndMaybeApplyParentheses(node, node.Operand);
                OutputText.Append(".Length");
                break;
            case ExpressionType.ArrayIndex:
                OutputText.Append('[');
                VisitAndMaybeApplyParentheses(node, node.Operand);
                OutputText.Append(']');
                break;
            case ExpressionType.ConvertChecked
                or ExpressionType.Convert:
                Visit(node.Operand);
                break;
            case ExpressionType.Negate:
                OutputText.Append('-');
                VisitAndMaybeApplyParentheses(node, node.Operand);
                break;
            case ExpressionType.UnaryPlus:
                OutputText.Append('+');
                VisitAndMaybeApplyParentheses(node, node.Operand);
                break;
            case ExpressionType.NegateChecked:
                OutputText.Append("checked(-");
                VisitAndMaybeApplyParentheses(node, node.Operand);
                break;
            case ExpressionType.Not:
                OutputText.Append('!');
                VisitAndMaybeApplyParentheses(node, node.Operand);
                break;
            case ExpressionType.Quote:
                OutputText.Append('"');
                VisitAndMaybeApplyParentheses(node, node.Operand);
                OutputText.Append('"');
                break;
            case ExpressionType.TypeAs:
                VisitAndMaybeApplyParentheses(node, node.Operand);
                OutputText.Append(" as ");
                OutputText.Append(node.Type.ToCSharpName());
                break;
            case ExpressionType.OnesComplement:
                OutputText.Append('~');
                VisitAndMaybeApplyParentheses(node, node.Operand);
                break;
            case ExpressionType.Increment or ExpressionType.PreIncrementAssign:
                OutputText.Append("++");
                VisitAndMaybeApplyParentheses(node, node.Operand);
                break;
            case ExpressionType.Decrement or ExpressionType.PreDecrementAssign:
                OutputText.Append("--");
                VisitAndMaybeApplyParentheses(node, node.Operand);
                break;
            case ExpressionType.PostIncrementAssign:
                VisitAndMaybeApplyParentheses(node, node.Operand);
                OutputText.Append("++");
                break;
            case ExpressionType.PostDecrementAssign:
                VisitAndMaybeApplyParentheses(node, node.Operand);
                OutputText.Append("--");
                break;
            case ExpressionType.Unbox
                or ExpressionType.IsTrue
                or ExpressionType.IsFalse:
                break;
            case ExpressionType.Throw:
                OutputText.Append("throw ");
                VisitAndMaybeApplyParentheses(node, node.Operand);
                break;
            default:
                VisitAndMaybeApplyParentheses(node, node.Operand);
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

        var isCheckedExpression = binaryExpression.NodeType is
            ExpressionType.AddChecked or
            ExpressionType.SubtractChecked;

        if (isCheckedExpression) OutputText.Append("checked(");

        VisitAndMaybeApplyParentheses(binaryExpression, binaryExpression.Left, true);

        OutputText.Append(' ');
        OutputText.Append(GetBinaryOperatorSymbol(binaryExpression.NodeType));
        OutputText.Append(' ');

        VisitAndMaybeApplyParentheses(binaryExpression, binaryExpression.Right, false);

        if (isCheckedExpression) OutputText.Append(')');

        return binaryExpression;
    }

    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
    {
        VisitAndMaybeApplyParentheses(node, node.Expression);
        OutputText.Append(" is ");
        OutputText.Append(node.TypeOperand.ToCSharpName());
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is not ConstantExpression)
        {
            if (node.Expression is null)
                OutputText.Append(node.Member.DeclaringType?.Name);
            else
            {
                VisitAndMaybeApplyParentheses(node, node.Expression);
            }

            OutputText.Append('.');
            OutputText.Append(node.Member.Name);
            return node;
        }

        OutputText.Append(node.Member.Name);
        return node;
    }

    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        VisitNewWithInitialization(node.NewExpression);
        OutputText.Append(" ");
        VisitMemberBindings(node.Bindings);

        return node;
    }

    private void VisitMemberBindings(IEnumerable<MemberBinding> bindings)
    {
        OutputText.Append("{ ");
        var isFirst = true;
        foreach (var binding in bindings)
        {
            OutputText.Append(isFirst ? "" : ", ");
            isFirst = false;

            switch (binding)
            {
                case MemberAssignment assignment:
                    OutputText.Append(assignment.Member.Name);
                    OutputText.Append(" = ");
                    Visit(assignment.Expression);
                    break;
                case MemberListBinding listBinding:
                    OutputText.Append(listBinding.Member.Name);
                    OutputText.Append(" = { ");
                    VisitSpreadOfExpressions(listBinding.Initializers.SelectMany(init => init.Arguments));
                    OutputText.Append(" }");
                    break;
                case MemberMemberBinding memberMemberBinding:
                    OutputText.Append(memberMemberBinding.Member.Name);
                    OutputText.Append(" = { ");
                    VisitMemberBindings(memberMemberBinding.Bindings);
                    OutputText.Append(" }");
                    break;
            }
        }
        OutputText.Append(" }");
    }

    protected override Expression VisitListInit(ListInitExpression node)
    {
        var arguments = node.Initializers.SelectMany(init => init.Arguments).ToArray();
        VisitNewWithInitialization(node.NewExpression);
        OutputText.Append(" { ");
        VisitSpreadOfExpressions(arguments);
        OutputText.Append(" }");
        return node;
    }

    protected override Expression VisitIndex(IndexExpression node)
    {
        OutputText.Append('[');
        Visit(node.Object);
        OutputText.Append(']');
        return node;
    }

    protected override Expression VisitNewArray(NewArrayExpression node)
    {
        OutputText.Append("new ");
        OutputText.Append(node.Type.GetElementType()!.ToCSharpName());
        if (node.Expressions.Count == 0)
        {
            OutputText.Append("[] {}");
            return node;
        }

        OutputText.Append("[] { ");

        VisitSpreadOfExpressions(node.Expressions);

        OutputText.Append(" }");

        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        var serialization = SerializeSupported(node.Value, node.Type) ?? node.Value.ToString();
        OutputText.Append(serialization);
        return node;
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        OutputText.Append('(');

        VisitSpreadOfParameterExpressions(node.Parameters);

        OutputText.Append(')');

        OutputText.Append(" => ");

        Visit(node.Body);

        return node;
    }

    private Expression VisitObjectIndex(MethodCallExpression node)
    {
        Visit(node.Object);
        OutputText.Append('[');
        VisitSpreadOfExpressions(node.Arguments);
        OutputText.Append(']');
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        switch (node.Method)
        {
            case { IsSpecialName: true, Name: "get_Item" }:
                return VisitObjectIndex(node);
            case { DeclaringType.FullName: "System.String", Name: nameof(string.Format) }:
                return VisitStringInterpolation(node);
            case { DeclaringType.FullName: $"Motiv.ExpressionTrees.{nameof(Display)}", Name: nameof(Display.AsValue) }:
                return VisitSerializeAsValue(ResolveMethodArguments(node).First());
        }

        var isExtensionMethod = node.Method.IsDefined(typeof(ExtensionAttribute), false);
        var instanceObject = isExtensionMethod ? node.Arguments[0] : node.Object;
        var arguments = isExtensionMethod ? node.Arguments.Skip(1) : node.Arguments;

        if (node.Method.IsStatic && !isExtensionMethod)
        {
            OutputText.Append(node.Method.DeclaringType!.ToCSharpName());
        }
        else
        {
            Visit(instanceObject);
        }

        OutputText.Append('.');
        OutputText.Append(node.ToCSharpName());
        OutputText.Append("(");
        VisitSpreadOfExpressions(arguments.ToArray());
        OutputText.Append(")");

        return node;
    }

    protected virtual Expression VisitSerializeAsValue(Expression node)
    {
        switch (node)
        {
            case ParameterExpression parameterExpression:
                OutputText.Append(parameterExpression.Name);
                break;
            case MemberExpression { Expression: ConstantExpression constantExpression } memberExpression:

                var (value, valueType) = constantExpression.GetConstantExpressionValue(memberExpression.Member.Name);
                if (IsSupported(value))
                {
                    var serializeSupported = SerializeSupported(value, valueType);
                    OutputText.Append(serializeSupported);
                    break;
                }
                OutputText.Append(value);
                break;
            case ConstantExpression constantExpression:
                var serialization = SerializeSupported(constantExpression.Value, constantExpression.Type) ??
                                    constantExpression.Value.ToString();
                OutputText.Append(serialization);
                break;
            default:
                Visit(node);
                break;
        }

        return node;
    }

    private Expression VisitStringInterpolation(MethodCallExpression node)
    {
        if (node.Arguments[0] is not ConstantExpression patternExpression || patternExpression.Type != typeof(string))
        {
            OutputText.Append("string.Format(");
            VisitSpreadOfExpressions(node.Arguments);
            OutputText.Append(')');
            return node;
        }

        var format = (string) patternExpression.Value!;
        var shouldSubstituteToken = false;
        var splitFormat = Regex.Split(format, @"(?<=[{])(\s*?\d+\s*?)(?=[:}])");

        OutputText.Append("$\"");
        foreach(var part in splitFormat)
        {
            if (shouldSubstituteToken)
            {
                Visit(node.Arguments[int.Parse(part)+1]);
            }
            else
                OutputText.Append(part);

            shouldSubstituteToken = !shouldSubstituteToken;
        }
        OutputText.Append('"');

        return node;
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        VisitAndMaybeApplyParentheses(node, node.Expression);

        OutputText.Append('(');
        VisitSpreadOfExpressions(node.Arguments);
        OutputText.Append(')');
        return node;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        OutputText.Append(node.Name);
        return node;
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        VisitAndMaybeApplyParentheses(node, node.Test);
        OutputText.Append(" ? ");
        VisitAndMaybeApplyParentheses(node, node.IfTrue);
        OutputText.Append(" : ");
        VisitAndMaybeApplyParentheses(node, node.IfFalse);

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

            OutputText.Append(", ");
            Visit(arg);
        }
    }

    private void VisitSpreadOfParameterExpressions(IEnumerable<ParameterExpression> parameterExpressions)
    {
        var isFirst = true;
        foreach (var parameterExpression in parameterExpressions)
        {
            OutputText.Append(isFirst ? "" : ", ");
            isFirst = false;
            OutputText.Append(parameterExpression.Type.ToCSharpName());
            OutputText.Append(' ');
            VisitParameter(parameterExpression);
        }
    }

    private void VisitAndMaybeApplyParentheses(Expression parent, Expression childToVisit, bool isLeft = true)
    {
        var needsParentheses = NeedsParentheses(parent, childToVisit, isLeft);
        if (needsParentheses)
            OutputText.Append('(');
        Visit(childToVisit);
        if (needsParentheses)
            OutputText.Append(')');
    }

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
            ExpressionType.Add or ExpressionType.AddChecked=> "+",
            ExpressionType.Subtract or ExpressionType.SubtractChecked => "-",
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

    private static bool NeedsParentheses(Expression parent, Expression child, bool isLeft = true)
    {
        child = ResolveVisibleNode(child);
        if (child is not BinaryExpression and not LambdaExpression and not ConditionalExpression)
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
                or ExpressionType.Subtract
                or ExpressionType.AddChecked
                or ExpressionType.SubtractChecked => 12,
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

    protected static string? SerializeSupported(object? obj, Type declaredType)
    {
        return obj switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            char ch => $"'{ch}'",
            string s => $"\"{s}\"",
            Guid guid => $"Guid.Parse({guid})",
            DateTime dateTime => $"DateTime.Parse(\"{dateTime:O}\")",
            DateTimeOffset dateTimeOffset => $"DateTimeOffset.Parse(\"{dateTimeOffset:O}\")",
            TimeSpan timespan => $"TimeSpan.Parse(\"{timespan}\")",
            Regex regex => $"new Regex(@\"{regex}\")",
            Type t => $"typeof({t.FullName})",
            _ when declaredType.IsEnum => $"{declaredType.Name}.{obj}",
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

    private IEnumerable<Expression> ResolveMethodArguments(MethodCallExpression node) =>
        node.Arguments.Select(ResolveVisibleNode);
}

internal class CSharpExpressionSerializer<T>(T model, ParameterExpression modelParameter) : CSharpExpressionSerializer()
{
    protected override Expression VisitSerializeAsValue(Expression node)
    {
        switch (node)
        {
            case ParameterExpression parameterExpression when parameterExpression == modelParameter:
                var serialization = SerializeSupported(model, parameterExpression.Type) ?? model!.ToString();
                OutputText.Append(serialization);
                return node;
            default:
                var body = Expression.Convert(node, typeof(object));
                var valueGetter = Expression.Lambda<Func<T, object>>(body, modelParameter).Compile();
                var value = valueGetter(model);
                OutputText.Append(SerializeSupported(value, node.Type) ?? value);
                return node;
        }
    }
}
