using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Motiv.ExpressionTreeProposition;

/// <summary>
/// Reads a value the model does not influence straight out of an expression tree by reflection, without compiling
/// it. A compile costs tens of microseconds and pins a <c>DynamicMethod</c>; a reflective read costs nanoseconds.
/// </summary>
internal static class CapturedValueReader
{
    /// <summary>
    /// Evaluates an expression that references no lambda parameter from outside itself. Member chains are read by
    /// reflection; anything else is interpreted, since a one-shot interpretation is ~50x cheaper than a JIT compile
    /// and pins no <c>DynamicMethod</c>.
    /// </summary>
    internal static bool TryEvaluate(Expression expression, out object? value)
    {
        value = null;
        try
        {
            return TryRead(expression, out value) || TryInterpret(expression, out value);
        }
        catch (Exception)
        {
            // The value is only for display text; the same failure resurfaces when the proposition is evaluated.
            return false;
        }
    }

    private static bool TryInterpret(Expression expression, out object? value)
    {
        if (FreeParameterDetector.HasFreeParameter(expression))
        {
            value = null;
            return false;
        }

        value = Expression
            .Lambda<Func<object?>>(Expression.Convert(expression, typeof(object)))
            .Compile(preferInterpretation: true)();
        return true;
    }

    /// <summary>
    /// Reads a chain of field and property accesses rooted in a constant (a closure, or a captured <c>this</c>) or
    /// in a static member, reading each link through the <see cref="MemberInfo" /> the node already carries. The same
    /// fast path EF Core's funcletizer takes; anything else, including a chain through <c>null</c>, is unreadable.
    /// </summary>
    internal static bool TryRead(Expression? expression, out object? value)
    {
        value = null;
        switch (expression)
        {
            case ConstantExpression constant:
                value = constant.Value;
                return true;
            case MemberExpression { Member: FieldInfo field } member when TryReadInstance(member.Expression, out var instance):
                value = field.GetValue(instance);
                return true;
            case MemberExpression { Member: PropertyInfo property } member when TryReadInstance(member.Expression, out var instance):
                value = GetPropertyValue(property, instance);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Reflection wraps a throwing getter's exception in a <see cref="TargetInvocationException" />; a compiled
    /// delegate does not. Rethrow the getter's own exception so callers see what they would have seen before.
    /// </summary>
    private static object? GetPropertyValue(PropertyInfo property, object? instance)
    {
        try
        {
            return property.GetValue(instance);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw Rethrow(exception.InnerException);
        }
    }

    /// <summary>Rethrows with the original stack trace; the return only satisfies the compiler.</summary>
    [ExcludeFromCodeCoverage]
    private static Exception Rethrow(Exception exception)
    {
        ExceptionDispatchInfo.Capture(exception).Throw();
        return exception;
    }

    private static bool TryReadInstance(Expression? instanceExpression, out object? instance)
    {
        instance = null;
        return instanceExpression is null
               || TryRead(instanceExpression, out instance) && instance is not null;
    }
}
