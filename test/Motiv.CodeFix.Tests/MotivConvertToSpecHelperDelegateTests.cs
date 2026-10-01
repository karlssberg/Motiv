using System.Reflection;
using Microsoft.CodeAnalysis.Testing;
using Shouldly;
using VerifyCS =
    Motiv.CodeFix.Tests.CSharpCodeFixVerifier<Motiv.Analyzer.MotivAnalyzer, Motiv.CodeFix.MotivCodeFixProvider>;

namespace Motiv.CodeFix.Tests;

/// <summary>
///     Expressions that call a method of the type they sit in. The spec is a class of its own, so it cannot reach a
///     private method, nor call an instance method without an instance; the containing type, which can, hands each
///     method to the spec's constructor as a delegate.
/// </summary>
public class MotivConvertToSpecHelperDelegateTests
{
    private const string Source = "Source.cs";

    private static readonly int[] Integers = [-1, 0, 1, 5, 12];

    private static readonly Dictionary<string, string> Helpers = new()
    {
        ["PrivateInstance"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsSmall(b)|];

            private bool IsSmall(int n) => n < _limit;

            private readonly int _limit = 10;
            """,
        ["ProtectedInstance"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsSmall(b)|];

            protected bool IsSmall(int n) => n < 10;
            """,
        ["PrivateStatic"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsSmall(b)|];

            private static bool IsSmall(int n) => n < 10;
            """,
        ["PublicInstance"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsSmall(b)|];

            public bool IsSmall(int n) => n < 10;
            """,
        ["InstanceAndStatic"] =
            """
            public bool Check(int a, int b) => [|IsPositive(a) && IsSmall(b)|];

            private bool IsPositive(int n) => n > 0;

            private static bool IsSmall(int n) => n < 10;
            """,
        ["Overloads"] =
            """
            public bool Check(int a, int b) => [|IsSmall(a) && IsSmall((long)b)|];

            private bool IsSmall(int n) => n < 10;

            private bool IsSmall(long n) => n < 5;
            """,
        ["CalledTwice"] =
            """
            public bool Check(int a, int b) => [|IsSmall(a) && IsSmall(b)|];

            private bool IsSmall(int n) => n < 10;
            """,
        ["OptionalArgumentOmitted"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsSmall(b)|];

            private bool IsSmall(int n, int limit = 5) => n < limit;
            """,
        ["NamedArgumentsReordered"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsBelow(limit: a, n: b)|];

            private bool IsBelow(int n, int limit) => n < limit;
            """,
        ["GenericHelperClosedByInference"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsDefault(b)|];

            private static bool IsDefault<T>(T value) => Equals(value, default(T));
            """,
        ["GenericHelperClosedExplicitly"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsDefault<int>(b)|];

            private bool IsDefault<T>(T value) => Equals(value, default(T));
            """,
        ["OptionalNullDefault"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsSmall(b)|];

            private bool IsSmall(int n, string label = null) => Equals(label, null) && n.CompareTo(10) < 0;
            """,
        ["OptionalEnumDefault"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsSmall(b)|];

            private bool IsSmall(int n, System.DayOfWeek day = System.DayOfWeek.Monday) => Equals(day, System.DayOfWeek.Monday) && n.CompareTo(10) < 0;
            """,
        ["OptionalLongDefault"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsSmall(b)|];

            private bool IsSmall(int n, long limit = 10) => limit.CompareTo(n) > 0;
            """,
        ["OptionalDoubleDefault"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsSmall(b)|];

            private bool IsSmall(int n, double limit = 10.5) => limit.CompareTo(n) > 0;
            """,
        ["DynamicParameter"] =
            """
            public bool Check(int a, int b) => [|a > 0 && IsSmall(b)|];

            private bool IsSmall(dynamic n) => ((int)n).CompareTo(10) < 0;
            """,
        ["NoArguments"] =
            """
            public bool Check(int a, int b) => [|a > b && IsReady()|];

            private bool IsReady() => _ready;

            private readonly bool _ready = true;
            """,
        ["CallerInfoArgument"] =
            """
            public bool Check(int a, int b) => [|a > b && IsCalledFrom()|];

            private bool IsCalledFrom([System.Runtime.CompilerServices.CallerMemberName] string caller = "") => Equals(caller, "Check");
            """,
        ["ResultOfACallCalledOn"] =
            """
            public bool Check(int a, int b) => [|a > 0 && Describe(b).Contains("1")|];

            private string Describe(int n) => n.ToString();
            """,
        ["ResultOfAnArgumentlessCallCalledOn"] =
            """
            public bool Check(int a, int b) => [|a > b && Name().Contains("heck")|];

            private string Name() => "Check";
            """,
        ["DelegateNamedLikeTheValue"] =
            """
            public bool Check(int a, int b) => [|b > 0 && B(b)|];

            private bool B(int n) => n.CompareTo(10) < 0;
            """,
        ["DelegateNamedLikeTheModel"] =
            """
            public bool Check(int a, int b) => [|a > 0 && M(b)|];

            private bool M(int n) => n.CompareTo(10) < 0;
            """,
        ["OverloadsOnArrays"] =
            """
            public bool Check(int a, int b) => [|IsFirstSmall(new[] { a }) && IsFirstSmall(new[] { b.ToString() })|];

            private bool IsFirstSmall(int[] values) => values[0].CompareTo(10) < 0;

            private bool IsFirstSmall(string[] values) => values[0].Length.Equals(1);
            """,
        ["MethodNamedLikeAKeyword"] =
            """
            public bool Check(int a, int b) => [|a > 0 && Is(b)|];

            private bool Is(int n) => n.CompareTo(10) < 0;
            """
    };

    [Theory]
    [InlineData("PrivateInstance")]
    [InlineData("ProtectedInstance")]
    [InlineData("PrivateStatic")]
    [InlineData("PublicInstance")]
    [InlineData("InstanceAndStatic")]
    [InlineData("Overloads")]
    [InlineData("CalledTwice")]
    [InlineData("OptionalArgumentOmitted")]
    [InlineData("NamedArgumentsReordered")]
    [InlineData("GenericHelperClosedByInference")]
    [InlineData("GenericHelperClosedExplicitly")]
    [InlineData("OptionalNullDefault")]
    [InlineData("OptionalEnumDefault")]
    [InlineData("OptionalLongDefault")]
    [InlineData("MethodNamedLikeAKeyword")]
    [InlineData("ResultOfACallCalledOn")]
    [InlineData("ResultOfAnArgumentlessCallCalledOn")]
    [InlineData("DelegateNamedLikeTheValue")]
    [InlineData("DelegateNamedLikeTheModel")]
    [InlineData("OverloadsOnArrays")]
    [InlineData("DynamicParameter")]
    [InlineData("OptionalDoubleDefault")]
    [InlineData("NoArguments")]
    [InlineData("CallerInfoArgument")]
    public async Task Should_pass_each_called_method_as_a_delegate_and_decide_as_the_expression_did(string helper)
    {
        var source =
          $$"""
            namespace MyNamespace;

            public class Checks
            {
                {{Helpers[helper].Replace("\n", "\n    ")}}
            }
            """;

        var outcome = await CodeFixHarness.ApplyFix(source);
        outcome.CompilerErrors.ShouldBeEmpty(outcome.FixedSource);

        var original = await CodeFixHarness.Load(source);
        var converted = await CodeFixHarness.Load(outcome.FixedSource);

        foreach (var a in Integers)
        foreach (var b in Integers)
        {
            Check(converted, a, b).ShouldBe(Check(original, a, b), $"({a}, {b})\n{outcome.FixedSource}");
        }
    }

    [Theory]
    [InlineData("private bool IsDefault(T value) => Equals(value, default(T));")]
    [InlineData("private static bool IsDefault(T value) => Equals(value, default(T));")]
    public async Task Should_pass_a_helper_of_a_generic_class_closed_over_its_type_parameter(string helper)
    {
        var source =
          $$"""
            namespace MyNamespace;

            public class Box<T>
            {
                public bool Holds(T value, int count) => [|count > 0 && IsDefault(value)|];

                {{helper}}
            }
            """;

        var outcome = await CodeFixHarness.ApplyFix(source);

        outcome.CompilerErrors.ShouldBeEmpty(outcome.FixedSource);
    }

    [Fact]
    public async Task Should_declare_a_class_type_parameter_that_only_a_helper_s_signature_names()
    {
        const string source =
            """
            namespace MyNamespace;

            public class Repository<T> where T : class
            {
                public bool Has(int id) => [|id > 0 && Find(id) != null|];

                private T Find(int id) => default;
            }
            """;

        var outcome = await CodeFixHarness.ApplyFix(source);

        outcome.CompilerErrors.ShouldBeEmpty(outcome.FixedSource);
    }

    [Fact]
    public async Task Should_name_the_delegate_type_as_the_file_already_imports_it()
    {
        const string source =
            """
            using System;

            namespace MyNamespace;

            public class Checks
            {
                public bool Check(int a, int b) => [|a > 0 && IsSmall(b)|];

                private static bool IsSmall(int n) => n.CompareTo(10) < 0;
            }
            """;

        var outcome = await CodeFixHarness.ApplyFix(source);

        outcome.CompilerErrors.ShouldBeEmpty(outcome.FixedSource);
        outcome.FixedSource.ShouldContain("(Func<int, bool> isSmall)");
        outcome.FixedSource.Split(["using System;"], StringSplitOptions.None).Length.ShouldBe(2, outcome.FixedSource);
    }

    [Fact]
    public async Task Should_leave_a_call_to_another_type_s_method_as_it_is()
    {
        const string source =
            """
            using static System.Math;

            namespace MyNamespace;

            public class Checks
            {
                public bool Check(int a, int b) => [|a > 0 && Max(a, b) > 5|];
            }
            """;

        var outcome = await CodeFixHarness.ApplyFix(source);

        outcome.CompilerErrors.ShouldBeEmpty(outcome.FixedSource);
        outcome.FixedSource.ShouldContain("Max(m.A, m.B) > 5");
    }

    [Fact]
    public async Task Should_pass_a_protected_method_inherited_from_a_base_class()
    {
        const string source =
            """
            namespace MyNamespace;

            public abstract class Rules
            {
                protected bool IsSmall(int n) => n.CompareTo(10) < 0;
            }

            public class Checks : Rules
            {
                public bool Check(int a, int b) => [|a > 0 && IsSmall(b)|];
            }
            """;

        var outcome = await CodeFixHarness.ApplyFix(source);

        outcome.CompilerErrors.ShouldBeEmpty(outcome.FixedSource);
    }

    [Fact]
    public async Task Should_hand_a_private_instance_method_to_the_spec_as_a_delegate()
    {
        const string booleanExpression = "quantity > 0 && HasRoomFor(quantity)";

        const string source =
          $$"""
            namespace MyNamespace;

            public class Basket
            {
                public bool CanAdd(int quantity) => {{booleanExpression}};

                private bool HasRoomFor(int quantity) => quantity.Equals(1);
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using System;
            using Motiv;

            namespace MyNamespace;

            public class Basket
            {
                private readonly CanAddProposition _canAddProposition;
                public Basket()
                {
                    _canAddProposition = new CanAddProposition(HasRoomFor);
                }

                public bool CanAdd(int quantity)
                {
                    // {{booleanExpression}}
                    var canAddResult = _canAddProposition.Evaluate(quantity);
                    return canAddResult.Satisfied;
                }

                private bool HasRoomFor(int quantity) => quantity.Equals(1);
            }

            public class CanAddProposition(Func<int, bool> hasRoomFor) : Spec<int>(() =>
            {
                var isQuantityPositive = Spec
                    .Build((int quantity) => quantity > 0)
                    .Create("quantity > 0");

                var hasRoomForQuantity = Spec
                    .Build((int quantity) => hasRoomFor(quantity))
                    .Create("HasRoomFor(quantity)");

                return isQuantityPositive.AndAlso(hasRoomForQuantity);
            });
            """;

        await VerifyFix(source, expectedTransformedCode, 5, 41, booleanExpression);
    }

    [Fact]
    public async Task Should_hold_the_spec_statically_when_every_method_it_calls_is_static()
    {
        const string booleanExpression = "quantity > 0 && IsSmall(quantity)";

        const string source =
          $$"""
            namespace MyNamespace;

            public class Basket
            {
                public bool CanAdd(int quantity) => {{booleanExpression}};

                private static bool IsSmall(int quantity) => quantity.Equals(1);
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using System;
            using Motiv;

            namespace MyNamespace;

            public class Basket
            {
                private static readonly CanAddProposition CanAddProposition = new(IsSmall);

                public bool CanAdd(int quantity)
                {
                    // {{booleanExpression}}
                    var canAddResult = CanAddProposition.Evaluate(quantity);
                    return canAddResult.Satisfied;
                }

                private static bool IsSmall(int quantity) => quantity.Equals(1);
            }

            public class CanAddProposition(Func<int, bool> isSmall) : Spec<int>(() =>
            {
                var isQuantityPositive = Spec
                    .Build((int quantity) => quantity > 0)
                    .Create("quantity > 0");

                var isSmallQuantity = Spec
                    .Build((int quantity) => isSmall(quantity))
                    .Create("IsSmall(quantity)");

                return isQuantityPositive.AndAlso(isSmallQuantity);
            });
            """;

        await VerifyFix(source, expectedTransformedCode, 5, 41, booleanExpression);
    }

    private static bool Check(Assembly assembly, int a, int b)
    {
        var type = assembly.GetType("MyNamespace.Checks")!;
        return (bool)type.GetMethod("Check")!.Invoke(Activator.CreateInstance(type), [a, b])!;
    }

    private static async Task VerifyFix(string source, string expected, int line, int column, string booleanExpression) =>
        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expected) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, line, column, line, column + booleanExpression.Length)
            }
        }.RunAsync();
}
