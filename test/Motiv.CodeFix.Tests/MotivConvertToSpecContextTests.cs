using Shouldly;

namespace Motiv.CodeFix.Tests;

/// <summary>
///     Places a convertible expression in every syntactic position C# allows one and asserts the
///     fix leaves code that compiles, with everything around the expression intact. Exact output is
///     the business of the other <c>MotivConvertToSpec*</c> suites; this one only asks whether the
///     fix survives the position.
/// </summary>
public class MotivConvertToSpecContextTests
{
    private const string MethodRewrite = "The fix rewrites the enclosing method instead of the expression";
    private const string RequiresMethod = "The fix requires the expression to sit in a class method";
    private const string UncapturedSymbol = "The generated spec cannot see a symbol the expression uses";

    private static readonly Dictionary<string, string> ConvertibleContexts = new()
    {
        ["ReturnStatement"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsInRange(int n)
                {
                    return [|n > 0 && n < 10|];
                }
            }
            """,
        ["ExpressionBodiedMethod"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsInRange(int n) => [|n > 0 && n < 10|];
            }
            """,
        ["StaticMethod"] =
            """
            namespace MyNamespace;

            public static class MyClass
            {
                public static bool IsInRange(int n) => [|n > 0 && n < 10|];
            }
            """,
        ["OutParameterAssignment"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public void Check(int n, out bool isInRange)
                {
                    isInRange = [|n > 0 && n < 10|];
                }
            }
            """,
        ["MultiStatementMethod"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public string Describe(int n)
                {
                    var prefix = "n is "; // must survive
                    var isInRange = [|n > 0 && n < 10|];
                    return prefix + (isInRange ? "in range" : "out of range"); // must survive
                }
            }
            """,
        ["AnnotatedMethod"] =
            """
            using System.Diagnostics.CodeAnalysis;

            namespace MyNamespace;

            public class MyClass
            {
                [ExcludeFromCodeCoverage] // must survive
                public bool IsInRange(int n)
                {
                    return [|n > 0 && n < 10|];
                }
            }
            """,
        ["IfCondition"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public int Clamp(int n)
                {
                    if ([|n > 0 && n < 10|])
                        return n; // must survive

                    return 0; // must survive
                }
            }
            """,
        ["WhileCondition"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public int CountDown(int n)
                {
                    while ([|n > 0 && n < 10|])
                        n--; // must survive

                    return n; // must survive
                }
            }
            """,
        ["ForCondition"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public int Sum(int limit)
                {
                    var total = 0; // must survive
                    for (var i = 0; [|i < limit && total < 100|]; i++)
                        total += i; // must survive

                    return total; // must survive
                }
            }
            """,
        ["ConditionalOperator"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public string Describe(int n)
                {
                    return [|n > 0 && n < 10|] ? "in range" : "out of range";
                }
            }
            """,
        ["MethodArgument"] =
            """
            using System;

            namespace MyNamespace;

            public class MyClass
            {
                public void Print(int n)
                {
                    Console.WriteLine([|n > 0 && n < 10|]);
                    Console.WriteLine(n); // must survive
                }
            }
            """,
        ["LambdaBody"] =
            """
            using System.Collections.Generic;
            using System.Linq;

            namespace MyNamespace;

            public class MyClass
            {
                public IEnumerable<int> InRange(IEnumerable<int> numbers)
                {
                    return numbers.Where(n => [|n > 0 && n < 10|]);
                }
            }
            """,
        ["QueryWhereClause"] =
            """
            using System.Collections.Generic;
            using System.Linq;

            namespace MyNamespace;

            public class MyClass
            {
                public IEnumerable<int> InRange(IEnumerable<int> numbers)
                {
                    return from n in numbers
                        where [|n > 0 && n < 10|]
                        select n;
                }
            }
            """,
        ["SwitchExpressionArm"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsAcceptable(bool strict, int n)
                {
                    return strict switch
                    {
                        true => [|n > 0 && n < 10|],
                        false => n > 0, // must survive
                    };
                }
            }
            """,
        ["SwitchExpressionWhenClause"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public string Describe(int n)
                {
                    return n switch
                    {
                        _ when [|n > 0 && n < 10|] => "in range",
                        _ => "out of range", // must survive
                    };
                }
            }
            """,
        ["SwitchStatementWhenClause"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public string Describe(object value)
                {
                    switch (value)
                    {
                        case int n when [|n > 0 && n < 10|]:
                            return "in range"; // must survive
                        default:
                            return "out of range"; // must survive
                    }
                }
            }
            """,
        ["YieldReturn"] =
            """
            using System.Collections.Generic;

            namespace MyNamespace;

            public class MyClass
            {
                public IEnumerable<bool> Checks(int n)
                {
                    yield return [|n > 0 && n < 10|];
                    yield return n == 42; // must survive
                }
            }
            """,
        ["AsyncMethod"] =
            """
            using System.Threading.Tasks;

            namespace MyNamespace;

            public class MyClass
            {
                public async Task<bool> IsInRangeAsync(int n)
                {
                    await Task.Yield(); // must survive
                    return [|n > 0 && n < 10|];
                }
            }
            """,
        ["ExpressionBodiedProperty"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                private int _count;

                public bool IsInRange => [|_count > 0 && _count < 10|];
            }
            """,
        ["PropertyGetter"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                private int _count;

                public bool IsInRange
                {
                    get { return [|_count > 0 && _count < 10|]; }
                }
            }
            """,
        ["InstanceFieldInitializer"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                private static readonly int Limit = 5;

                private readonly bool _isInRange = [|Limit > 0 && Limit < 10|];
            }
            """,
        ["StaticFieldInitializer"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                private static readonly int Limit = 5;

                private static readonly bool IsInRange = [|Limit > 0 && Limit < 10|];
            }
            """,
        ["ConstructorBody"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public MyClass(int n)
                {
                    IsInRange = [|n > 0 && n < 10|];
                }

                public bool IsInRange { get; }
            }
            """,
        ["ConstructorInitializer"] =
            """
            namespace MyNamespace;

            public class Base(bool isInRange)
            {
                public bool IsInRange { get; } = isInRange;
            }

            public class Derived : Base
            {
                public Derived(int n) : base([|n > 0 && n < 10|])
                {
                }
            }
            """,
        ["LocalFunction"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public int CountInRange(int[] numbers)
                {
                    var count = 0; // must survive
                    foreach (var number in numbers)
                        if (IsInRange(number)) count++; // must survive

                    return count; // must survive

                    bool IsInRange(int n) => [|n > 0 && n < 10|];
                }
            }
            """,
        ["StaticLocalFunction"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public bool Check(int value)
                {
                    return IsInRange(value); // must survive

                    static bool IsInRange(int n) => [|n > 0 && n < 10|];
                }
            }
            """,
        ["ConversionOperator"] =
            """
            namespace MyNamespace;

            public class Range(int value)
            {
                public int Value { get; } = value;

                public static explicit operator bool(Range range) => [|range is not null && range.Value > 0|];
            }
            """,
        ["Record"] =
            """
            namespace MyNamespace;

            public record Limits(int Max)
            {
                public bool IsInRange(int n) => [|n > 0 && n < 10|];
            }
            """,
        ["Struct"] =
            """
            namespace MyNamespace;

            public struct Limits
            {
                public bool IsInRange(int n) => [|n > 0 && n < 10|];
            }
            """,
        ["NestedClass"] =
            """
            namespace MyNamespace;

            public class Outer
            {
                public class Inner
                {
                    public bool IsInRange(int n) => [|n > 0 && n < 10|];
                }
            }
            """,
        ["BlockNamespace"] =
            """
            namespace MyNamespace
            {
                public class MyClass
                {
                    public bool IsInRange(int n) => [|n > 0 && n < 10|];
                }
            }
            """,
        ["GenericMethod"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public bool AreBothMissing<T>(T first, T second) where T : class => [|first is null && second is null|];
            }
            """,
        ["InstancePropertyReference"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public int Count { get; set; }

                public bool IsInRange() => [|Count > 0 && Count < 10|];
            }
            """,
    };

    private static readonly Dictionary<string, string> NonConvertibleContexts = new()
    {
        ["ConstField"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                private const int Limit = 5;

                private const bool IsInRange = [|Limit > 0 && Limit < 10|];
            }
            """,
        ["ConstLocal"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                private const int Limit = 5;

                public bool Check()
                {
                    const bool isInRange = [|Limit > 0 && Limit < 10|];
                    return isInRange;
                }
            }
            """,
        ["AttributeArgument"] =
            """
            using System;

            namespace MyNamespace;

            public class MyClass
            {
                private const int Limit = 5;

                [Obsolete("Use something else", [|Limit > 0 && Limit < 10|])]
                public void Check()
                {
                }
            }
            """,
        ["DefaultParameterValue"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                private const int Limit = 5;

                public bool Check(bool isInRange = [|Limit > 0 && Limit < 10|]) => isInRange;
            }
            """,
        ["CaseLabel"] =
            """
            namespace MyNamespace;

            public class MyClass
            {
                private const int Limit = 5;

                public string Describe(bool flag)
                {
                    switch (flag)
                    {
                        case [|Limit > 0 && Limit < 10|]:
                            return "matches";
                        default:
                            return "differs";
                    }
                }
            }
            """,
        ["ExpressionTreeLambda"] =
            """
            using System;
            using System.Linq.Expressions;

            namespace MyNamespace;

            public class MyClass
            {
                public Expression<Func<int, bool>> IsInRange() => n => [|n > 0 && n < 10|];
            }
            """,
    };

    [Theory]
    [InlineData("ReturnStatement")]
    [InlineData("ExpressionBodiedMethod")]
    [InlineData("StaticMethod")]
    [InlineData("OutParameterAssignment")]
    [InlineData("BlockNamespace")]
    [InlineData("NestedClass")]
    [InlineData("MultiStatementMethod", Skip = MethodRewrite)]
    [InlineData("AnnotatedMethod", Skip = MethodRewrite)]
    [InlineData("IfCondition", Skip = MethodRewrite)]
    [InlineData("WhileCondition", Skip = MethodRewrite)]
    [InlineData("ForCondition", Skip = MethodRewrite)]
    [InlineData("ConditionalOperator", Skip = MethodRewrite)]
    [InlineData("MethodArgument", Skip = MethodRewrite)]
    [InlineData("LambdaBody", Skip = MethodRewrite)]
    [InlineData("SwitchExpressionArm", Skip = MethodRewrite)]
    [InlineData("SwitchExpressionWhenClause", Skip = MethodRewrite)]
    [InlineData("YieldReturn", Skip = MethodRewrite)]
    [InlineData("AsyncMethod", Skip = MethodRewrite)]
    [InlineData("LocalFunction", Skip = MethodRewrite)]
    [InlineData("StaticLocalFunction", Skip = MethodRewrite)]
    [InlineData("ExpressionBodiedProperty", Skip = RequiresMethod)]
    [InlineData("PropertyGetter", Skip = RequiresMethod)]
    [InlineData("InstanceFieldInitializer", Skip = RequiresMethod)]
    [InlineData("StaticFieldInitializer", Skip = RequiresMethod)]
    [InlineData("ConstructorBody", Skip = RequiresMethod)]
    [InlineData("ConstructorInitializer", Skip = RequiresMethod)]
    [InlineData("ConversionOperator", Skip = RequiresMethod)]
    [InlineData("Record", Skip = RequiresMethod)]
    [InlineData("Struct", Skip = RequiresMethod)]
    [InlineData("QueryWhereClause", Skip = UncapturedSymbol)]
    [InlineData("SwitchStatementWhenClause", Skip = UncapturedSymbol)]
    [InlineData("GenericMethod", Skip = UncapturedSymbol)]
    [InlineData("InstancePropertyReference", Skip = UncapturedSymbol)]
    public async Task Should_produce_compiling_code_and_keep_surrounding_code_when_converting_expression_in_context(
        string context)
    {
        var outcome = await CodeFixHarness.ApplyFix(ConvertibleContexts[context]);

        outcome.CompilerErrors.ShouldBeEmpty(outcome.FixedSource);
        outcome.MissingSurvivors.ShouldBeEmpty(outcome.FixedSource);
    }

    [Theory]
    [InlineData("ConstField")]
    [InlineData("ConstLocal")]
    [InlineData("AttributeArgument")]
    [InlineData("DefaultParameterValue")]
    [InlineData("CaseLabel")]
    [InlineData("ExpressionTreeLambda")]
    public async Task Should_not_flag_expression_when_context_cannot_hold_a_spec_evaluation(string context)
    {
        var spans = await CodeFixHarness.GetDiagnosticSpans(NonConvertibleContexts[context]);

        spans.ShouldBeEmpty();
    }

    [Fact]
    public void Should_cover_every_context_with_a_test_case()
    {
        var convertibleCases = GetInlineDataContexts(
            nameof(Should_produce_compiling_code_and_keep_surrounding_code_when_converting_expression_in_context));
        var nonConvertibleCases = GetInlineDataContexts(
            nameof(Should_not_flag_expression_when_context_cannot_hold_a_spec_evaluation));

        convertibleCases.ShouldBe(ConvertibleContexts.Keys, ignoreOrder: true);
        nonConvertibleCases.ShouldBe(NonConvertibleContexts.Keys, ignoreOrder: true);
    }

    private static IEnumerable<string> GetInlineDataContexts(string methodName) =>
        typeof(MotivConvertToSpecContextTests)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(InlineDataAttribute), inherit: false)
            .Cast<InlineDataAttribute>()
            .SelectMany(data => data.GetData(null!))
            .Select(row => (string)row[0]);
}
