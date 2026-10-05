using System.Reflection;

namespace Motiv.Tests;

/// <summary>
/// Every public spec combinator refuses a null operand when the spec is built, naming the parameter, as the
/// result combinators do since #318. Before this, a null operand was accepted and failed only at evaluation
/// — after the left operand's predicate had already run — with an exception that named neither the caller's
/// mistake nor the parameter.
/// </summary>
public class SpecCombinatorNullHandlingTests
{
    private static readonly string[] CombinatorNames =
    [
        "And", "AndAlso", "Or", "OrElse", "XOr",
        "AndConcurrently", "OrConcurrently", "XOrConcurrently",
        "op_BitwiseAnd", "op_BitwiseOr", "op_ExclusiveOr", "op_LogicalNot"
    ];

    private static readonly Type[] SpecTypes =
    [
        typeof(SpecBase<int>),
        typeof(SpecBase<int, string>),
        typeof(PolicyBase<int, string>),
        typeof(AsyncSpecBase<int>),
        typeof(AsyncSpecBase<int, string>),
        typeof(AsyncPolicyBase<int, string>),
        typeof(ExpressionSpecBase<int, string>),
        typeof(ExpressionPolicyBase<int, string>)
    ];

    private static readonly Lazy<object[]> Operands = new(CreateOperands);

    private static object[] CreateOperands()
    {
        var policy = Spec.Build((int n) => n > 0).Create("is positive");
        var expressionPolicy = Spec.From((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create();
        var expressionSpec = Spec.From((int n) => n > 0).Create("is positive expression");
        var asyncPolicy = policy.ToAsyncSpec();

        // Ordered most-derived first, so each slot is filled by the most specific operand it accepts.
        return
        [
            expressionPolicy,
            expressionSpec,
            policy,
            policy & policy,
            asyncPolicy,
            asyncPolicy & asyncPolicy
        ];
    }

    private static object OperandFor(Type parameterType) =>
        Operands.Value.First(parameterType.IsInstanceOfType);

    private static IEnumerable<MethodInfo> Combinators() =>
        SpecTypes
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => CombinatorNames.Contains(method.Name) && method.GetParameters().Length > 0);

    private static string Describe(MethodInfo method) =>
        $"{method.DeclaringType!.Name}.{method.Name}" +
        $"({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})";

    public static TheoryData<string, int> NullableOperandSlots()
    {
        var data = new TheoryData<string, int>();
        foreach (var method in Combinators())
            for (var index = 0; index < method.GetParameters().Length; index++)
                data.Add(Describe(method), index);

        return data;
    }

    [Theory]
    [MemberData(nameof(NullableOperandSlots))]
    public void Should_reject_a_null_operand_naming_the_parameter(string combinator, int nullIndex)
    {
        // Arrange
        var method = Combinators().Single(m => Describe(m) == combinator);
        if (method.IsGenericMethodDefinition)
            method = method.MakeGenericMethod(typeof(ExpressionSpecBase<int, string>));
        var parameters = method.GetParameters();
        var arguments = parameters
            .Select((parameter, index) => index == nullIndex ? null : OperandFor(parameter.ParameterType))
            .ToArray();
        var target = method.IsStatic ? null : OperandFor(method.DeclaringType!);
        var paramName = parameters[nullIndex].Name;

        // Act
        var act = () =>
        {
            try
            {
                method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        };

        // Assert
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe<string?>(paramName);
        exception.Message.ShouldContain($"'{paramName}' cannot be null");
    }

    [Fact]
    public void Should_find_every_combinator_family_on_every_spec_type()
    {
        // Arrange
        var found = Combinators().Select(Describe).ToList();

        // Assert — a guard against the theory above silently enumerating nothing.
        found.ShouldContain("AsyncSpecBase`2.AndConcurrently(AsyncSpecBase`2 spec)");
        found.ShouldContain("AsyncSpecBase`2.op_BitwiseAnd(AsyncSpecBase`2 left, SpecBase`2 right)");
        found.ShouldContain("SpecBase`1.op_ExclusiveOr(SpecBase`1 left, SpecBase`1 right)");
        found.ShouldContain("PolicyBase`2.OrElse(PolicyBase`2 alternative)");
        found.ShouldContain("AsyncPolicyBase`2.AndAlso(AsyncPolicyBase`2 other)");
        found.ShouldContain("ExpressionPolicyBase`2.XOr(ExpressionSpecBase`2 spec)");
        found.ShouldContain("ExpressionSpecBase`2.op_BitwiseOr(ExpressionSpecBase`2 left, ExpressionPolicyBase`2 right)");
        foreach (var type in SpecTypes)
            found.ShouldContain(name => name.StartsWith(type.Name + "."), $"no combinator found on {type.Name}");
    }

    [Fact]
    public void Should_reject_a_null_concurrent_operand_when_the_spec_is_built()
    {
        // Arrange
        var left = Spec.Build((int n) => n > 0).Create("is positive").ToAsyncSpec();

        // Act
        var act = () => left.AndConcurrently((AsyncSpecBase<int, string>)null!);

        // Assert
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe<string?>("spec");
        exception.Message.ShouldContain("'spec' cannot be null");
    }

    [Fact]
    public void Should_name_the_right_operand_when_an_operator_is_given_null()
    {
        // Arrange
        var left = Spec.Build((int n) => n > 0).Create("is positive");
        SpecBase<int, string> right = null!;

        // Act
        var act = () => left & right;

        // Assert
        var exception = act.ShouldThrow<ArgumentNullException>();
        exception.ParamName.ShouldBe<string?>("right");
        exception.Message.ShouldContain("'right' cannot be null");
    }
}
