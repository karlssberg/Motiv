#if NET8_0_OR_GREATER
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Serialization.Expressions;

/// <summary>
/// Turns a checked leaf into the expression tree a developer would have written by hand in
/// <c>Spec.From</c>: native operator nodes over operands the lattice has already made the same
/// type, typed constants, checked arithmetic, and null-conditional navigation wherever the model
/// says a path may be null.
/// </summary>
internal sealed class LeafCompiler
{
    private readonly LeafAnalysis _analysis;
    private readonly IReadOnlyDictionary<string, object?> _values;
    private readonly ParameterExpression _model;
    private readonly Dictionary<string, ParameterExpression> _variables = new(StringComparer.Ordinal);

    private LeafCompiler(LeafAnalysis analysis, IReadOnlyDictionary<string, object?> values, ParameterExpression model)
    {
        _analysis = analysis;
        _values = values;
        _model = model;
    }

    public static SpecBase<TModel, string> Compile<TModel>(
        LeafNode root, LeafAnalysis analysis, IReadOnlyDictionary<string, object?> parameterValues, string leafText)
    {
        var model = Expression.Parameter(typeof(TModel), "model");
        var compiler = new LeafCompiler(analysis, parameterValues, model);
        var body = compiler.AsBool(compiler.Visit(root));
        var lambda = Expression.Lambda<Func<TModel, bool>>(body, model);
        return Spec.From(lambda).Create(leafText);
    }

    private Type TypeOf(LeafNode node) => _analysis.Types[node];

    private static bool IsNullable(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    private static Expression Unwrap(Expression value) =>
        Nullable.GetUnderlyingType(value.Type) is null ? value : Expression.Property(value, "Value");

    private Expression AsBool(Expression value) =>
        value.Type == typeof(bool) ? value : Expression.Coalesce(value, Expression.Constant(false));

    private Expression Visit(LeafNode node) => node switch
    {
        NumberLiteral n => Typed(NumericLattice.Constant(n.Text, NumericLattice.KindOf(TypeOf(n))!.Value), TypeOf(n)),
        StringLiteral s => Expression.Constant(s.Value, typeof(string)),
        BoolLiteral b => Expression.Constant(b.Value),
        NullLiteral => Expression.Constant(null, typeof(object)),
        ParameterRef p => Parameter(p),
        Identifier i => Identifier(i),
        MemberAccess m => Member(m),
        MethodCall c => Call(c),
        Unary u => u.Operator == "!" ? Expression.Not(AsBool(Visit(u.Operand))) : Expression.NegateChecked(Visit(u.Operand)),
        Binary b => Binary(b),
        _ => throw new InvalidOperationException($"unexpected leaf node {node.GetType().Name}"),
    };

    private static Expression Typed(Expression constant, Type target) =>
        constant.Type == target ? constant : Expression.Convert(constant, target);

    private Expression Parameter(ParameterRef p)
    {
        var target = TypeOf(p);
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        var value = _values[p.Name];
        var converted = value is null ? null : Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        return Typed(Expression.Constant(converted, underlying), target);
    }

    private Expression Identifier(Identifier i)
    {
        if (_variables.TryGetValue(i.Name, out var variable))
            return variable;
        var member = LeafScope.FindMember(_model.Type, i.Name)!;
        return Expression.MakeMemberAccess(_model, member);
    }

    private Expression Member(MemberAccess m)
    {
        var target = Visit(m.Target);
        var member = LeafScope.FindMember(Nullable.GetUnderlyingType(target.Type) ?? target.Type, m.Name)!;
        return IsNullable(target.Type)
            ? NullConditionalExpression.Create(target, t => Expression.MakeMemberAccess(Unwrap(t), member))
            : Expression.MakeMemberAccess(target, member);
    }

    private Expression Call(MethodCall c)
    {
        var target = Visit(c.Target);
        if (c.Method == "equalsIgnoreCase")
        {
            var equals = typeof(string).GetMethod(nameof(string.Equals), [typeof(string), typeof(string), typeof(StringComparison)])!;
            return Expression.Call(equals, target, Visit(c.Arguments[0]), Expression.Constant(StringComparison.OrdinalIgnoreCase));
        }

        var element = LeafScope.ElementType(Nullable.GetUnderlyingType(target.Type) ?? target.Type)!;
        Func<Expression, Expression> build = c.Method switch
        {
            "count" => t => Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), [element], t),
            "where" => t => Expression.Call(typeof(Enumerable), nameof(Enumerable.Where), [element], t, Predicate((Lambda)c.Arguments[0], element)),
            "any" => t => Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), [element], t, Predicate((Lambda)c.Arguments[0], element)),
            "all" => t => Expression.Call(typeof(Enumerable), nameof(Enumerable.All), [element], t, Predicate((Lambda)c.Arguments[0], element)),
            _ => t => Aggregate(c, t, element),
        };
        return IsNullable(target.Type) ? NullConditionalExpression.Create(target, build) : build(target);
    }

    private LambdaExpression Predicate(Lambda lambda, Type element)
    {
        var parameter = Expression.Parameter(element, lambda.Parameter);
        _variables[lambda.Parameter] = parameter;
        var body = AsBool(Visit(lambda.Body));
        _variables.Remove(lambda.Parameter);
        return Expression.Lambda(body, parameter);
    }

    private Expression Aggregate(MethodCall c, Expression target, Type element)
    {
        var lambda = (Lambda)c.Arguments[0];
        var resultType = Nullable.GetUnderlyingType(TypeOf(c)) ?? TypeOf(c);
        var kind = NumericLattice.KindOf(resultType)!.Value;
        var parameter = Expression.Parameter(element, lambda.Parameter);
        _variables[lambda.Parameter] = parameter;
        var body = Visit(lambda.Body);
        _variables.Remove(lambda.Parameter);
        body = NumericLattice.Widen(body, kind);
        var selector = Expression.Lambda(body, parameter);
        var name = c.Method switch { "sum" => nameof(Enumerable.Sum), "min" => nameof(Enumerable.Min), _ => nameof(Enumerable.Max) };
        var method = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == name && m.IsGenericMethodDefinition && m.GetParameters().Length == 2
                         && m.GetParameters()[1].ParameterType.GetGenericArguments()[1] == body.Type)
            .MakeGenericMethod(element);
        return Expression.Call(method, target, selector);
    }

    private Expression Binary(Binary b)
    {
        var left = Visit(b.Left);
        var right = Visit(b.Right);

        switch (b.Operator)
        {
            case "&&": return Expression.AndAlso(AsBool(left), AsBool(right));
            case "||": return Expression.OrElse(AsBool(left), AsBool(right));
        }

        if (b.Left is NullLiteral || b.Right is NullLiteral)
        {
            var side = b.Left is NullLiteral ? right : left;
            var comparison = Expression.Equal(side, Expression.Constant(null, side.Type));
            return b.Operator == "==" ? comparison : Expression.Not(comparison);
        }

        if (NumericLattice.KindOf(left.Type) is { } lk && NumericLattice.KindOf(right.Type) is { } rk)
        {
            var kind = NumericLattice.Join(lk, rk) ?? NumericLattice.KindOf(TypeOf(b))!.Value;
            var lifted = IsNullable(left.Type) || IsNullable(right.Type);
            left = NumericLattice.Widen(Lift(left, lifted), kind);
            right = NumericLattice.Widen(Lift(right, lifted), kind);
        }

        return b.Operator switch
        {
            "==" => Expression.Equal(left, right),
            "!=" => Expression.NotEqual(left, right),
            "<" => Expression.LessThan(left, right),
            "<=" => Expression.LessThanOrEqual(left, right),
            ">" => Expression.GreaterThan(left, right),
            ">=" => Expression.GreaterThanOrEqual(left, right),
            "+" => Expression.AddChecked(left, right),
            "-" => Expression.SubtractChecked(left, right),
            "*" => Expression.MultiplyChecked(left, right),
            _ => Expression.Divide(left, right),
        };
    }

    private static Expression Lift(Expression value, bool lifted)
    {
        if (!lifted || !value.Type.IsValueType || Nullable.GetUnderlyingType(value.Type) is not null) return value;
        return Expression.Convert(value, typeof(Nullable<>).MakeGenericType(value.Type));
    }
}
#endif
