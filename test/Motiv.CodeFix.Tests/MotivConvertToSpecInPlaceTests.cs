using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Motiv.CodeFix.Tests.CSharpCodeFixVerifier<Motiv.Analyzer.MotivAnalyzer, Motiv.CodeFix.MotivCodeFixProvider>;

namespace Motiv.CodeFix.Tests;

public class MotivConvertToSpecInPlaceTests
{
    private const string Source = "Source.cs";

    [Fact]
    public async Task Should_replace_only_the_argument_when_expression_is_a_method_argument()
    {
        const string booleanExpression = "n > 0 && n < 10";

        const string source =
          $$"""
            using System;

            namespace MyNamespace;

            public class MyClass
            {
                public void Print(int n)
                {
                    Console.WriteLine({{booleanExpression}});
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using System;
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly NProposition NProposition = new();

                public void Print(int n)
                {
                    Console.WriteLine(NProposition.Matches(n));
                }
            }

            public class NProposition() : Spec<int>(() =>
                Spec.Build((int n) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 9, 27, 9, 27 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_replace_only_the_condition_when_expression_is_an_if_condition()
    {
        const string booleanExpression = "n > 0 && n < 10";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public int Clamp(int n)
                {
                    if ({{booleanExpression}})
                        return n;

                    return 0;
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly NProposition NProposition = new();

                public int Clamp(int n)
                {
                    if (NProposition.Matches(n))
                        return n;

                    return 0;
                }
            }

            public class NProposition() : Spec<int>(() =>
                Spec.Build((int n) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 13, 7, 13 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_replace_only_the_declaration_and_keep_the_other_statements_of_the_method()
    {
        const string booleanExpression = "n > 0 && n < 10";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public string Describe(int n)
                {
                    var prefix = "n is ";
                    var isInRange = {{booleanExpression}};
                    return prefix + (isInRange ? "in range" : "out of range");
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly IsInRangeProposition IsInRangeProposition = new();

                public string Describe(int n)
                {
                    var prefix = "n is ";
                    // {{booleanExpression}}
                    var isInRangeResult = IsInRangeProposition.Evaluate(n);
                    var isInRange = isInRangeResult.Satisfied;
                    return prefix + (isInRange ? "in range" : "out of range");
                }
            }

            public class IsInRangeProposition() : Spec<int>(() =>
                Spec.Build((int n) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 8, 25, 8, 25 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_evaluate_in_place_so_the_debug_tap_fires_when_converting_with_debug_output()
    {
        const string booleanExpression = "n > 0 && n < 10";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public int Clamp(int n)
                {
                    if ({{booleanExpression}})
                        return n;

                    return 0;
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using System.Diagnostics;
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly SpecBase<int, string> NProposition = new NProposition()
                    .Tap((model, result) =>
                        Debug.WriteLine($"[Motiv] NProposition | Model: {model} | Satisfied: {result.Satisfied} | Reason: {result.Reason}"));

                public int Clamp(int n)
                {
                    if (NProposition.Evaluate(n).Satisfied)
                        return n;

                    return 0;
                }
            }

            public class NProposition() : Spec<int>(() =>
                Spec.Build((int n) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 13, 7, 13 + booleanExpression.Length)
            },
            CodeActionEquivalenceKey = "ConvertToSpecWithDebugOutput"
        }.RunAsync();
    }

    [Fact]
    public async Task Should_hold_the_spec_in_a_static_field_when_expression_is_a_field_initializer_of_a_struct()
    {
        const string booleanExpression = "Limit > 0 && Limit < 10";

        const string source =
          $$"""
            namespace MyNamespace;

            public struct Limits
            {
                private static readonly int Limit = 5;

                public static readonly bool IsInRange = {{booleanExpression}};
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public struct Limits
            {
                private static readonly int Limit = 5;
                private static readonly IsInRangeProposition IsInRangeProposition = new();

                public static readonly bool IsInRange = IsInRangeProposition.Matches(Limit);
            }

            public class IsInRangeProposition() : Spec<int>(() =>
                Spec.Build((int Limit) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 45, 7, 45 + booleanExpression.Length)
            }
        }.RunAsync();
    }
}
