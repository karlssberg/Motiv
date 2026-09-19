using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

/// <summary>
/// <c>target?.access</c> as an expression-tree node: reduces to binding <c>target</c> to a single
/// temporary and branching on that temporary being null, and is printed by the C# serializer as the
/// null-conditional it stands for rather than as the block it reduces to.
/// </summary>
public sealed class NullConditionalExpression : Expression
{
    private NullConditionalExpression(Expression target, Expression access, ParameterExpression receiver, Type type)
    {
        Target = target;
        Access = access;
        Receiver = receiver;
        Type = type;
    }

    /// <summary>The receiver that may be null.</summary>
    public Expression Target { get; }

    /// <summary>The member access or call, built over <see cref="Receiver" /> as if it were non-null.</summary>
    public Expression Access { get; }

    /// <summary>
    /// The placeholder parameter that <see cref="Access" /> is closed over in place of the evaluated
    /// <see cref="Target" />. Never itself evaluated — <see cref="Reduce" /> substitutes it with a
    /// temporary bound to <see cref="Target" />'s single evaluation, and the serializer elides it when
    /// printing <see cref="Access" />.
    /// </summary>
    public ParameterExpression Receiver { get; }

    /// <inheritdoc />
    public override Type Type { get; }

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override bool CanReduce => true;

    /// <summary>Builds <c>target?.access</c>; the result type is lifted to nullable for value types.</summary>
    public static NullConditionalExpression Create(Expression target, Func<Expression, Expression> access)
    {
        var receiver = Parameter(target.Type, "it");
        var accessed = access(receiver);
        var type = accessed.Type.IsValueType && Nullable.GetUnderlyingType(accessed.Type) is null
            ? typeof(Nullable<>).MakeGenericType(accessed.Type)
            : accessed.Type;
        return new NullConditionalExpression(target, accessed, receiver, type);
    }

    /// <inheritdoc />
    public override Expression Reduce()
    {
        var target = Parameter(Target.Type, "target");
        var access = new ReceiverSubstitution(Receiver, target).Visit(Access);

        return Block(
            Type,
            new[] { target },
            Assign(target, Target),
            Condition(
                Equal(target, Constant(null, target.Type)),
                Default(Type),
                access.Type == Type ? access : Convert(access, Type)));
    }

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var target = visitor.Visit(Target);
        var access = visitor.Visit(Access);
        return ReferenceEquals(target, Target) && ReferenceEquals(access, Access)
            ? this
            : new NullConditionalExpression(target, access, Receiver, Type);
    }

    /// <summary>Replaces every occurrence of a placeholder parameter with another expression.</summary>
    private sealed class ReceiverSubstitution(ParameterExpression receiver, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == receiver ? replacement : base.VisitParameter(node);
    }
}
