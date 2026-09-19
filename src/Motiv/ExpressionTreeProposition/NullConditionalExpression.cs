using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

/// <summary>
/// <c>target?.access</c> as an expression-tree node: reduces to
/// <c>target == null ? default : access(target)</c>, and is printed by the C# serializer as the
/// null-conditional it stands for rather than as the conditional it reduces to.
/// </summary>
public sealed class NullConditionalExpression : Expression
{
    private NullConditionalExpression(Expression target, Expression access, Type type)
    {
        Target = target;
        Access = access;
        Type = type;
    }

    /// <summary>The receiver that may be null.</summary>
    public Expression Target { get; }

    /// <summary>The member access or call, built over <see cref="Target" /> as if it were non-null.</summary>
    public Expression Access { get; }

    /// <inheritdoc />
    public override Type Type { get; }

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override bool CanReduce => true;

    /// <summary>Builds <c>target?.access</c>; the result type is lifted to nullable for value types.</summary>
    public static NullConditionalExpression Create(Expression target, Func<Expression, Expression> access)
    {
        var accessed = access(target);
        var type = accessed.Type.IsValueType && Nullable.GetUnderlyingType(accessed.Type) is null
            ? typeof(Nullable<>).MakeGenericType(accessed.Type)
            : accessed.Type;
        return new NullConditionalExpression(target, accessed, type);
    }

    /// <inheritdoc />
    public override Expression Reduce() =>
        Condition(
            Equal(Target, Constant(null, Target.Type)),
            Default(Type),
            Access.Type == Type ? Access : Convert(Access, Type));

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var target = visitor.Visit(Target);
        var access = visitor.Visit(Access);
        return ReferenceEquals(target, Target) && ReferenceEquals(access, Access)
            ? this
            : new NullConditionalExpression(target, access, Type);
    }
}
