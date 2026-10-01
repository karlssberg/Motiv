using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Motiv.CodeFix.Tests.CSharpCodeFixVerifier<Motiv.Analyzer.MotivAnalyzer, Motiv.CodeFix.MotivCodeFixProvider>;

namespace Motiv.CodeFix.Tests;

/// <summary>
///     Converting an expression must only rewrite the statement that holds it — every other statement
///     in the method body survives the fix.
/// </summary>
public class MotivConvertToSpecStatementPreservationTests
{
    private const string Source = "Source.cs";
    private const string BooleanExpression = "text.Length > 0";

    private const string PropositionClass =
        $$"""
          public class IsValidProposition() : Spec<string>(() =>
              Spec.Build((string text) => {{BooleanExpression}})
                  .Create("{{BooleanExpression}}"));
          """;

    [Fact]
    public async Task Should_keep_guard_statement_before_converted_return()
    {
        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(string text)
                {
                    System.ArgumentNullException.ThrowIfNull(text);

                    return {{BooleanExpression}};
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly IsValidProposition IsValidProposition = new();

                public bool IsValid(string text)
                {
                    System.ArgumentNullException.ThrowIfNull(text);

                    // {{BooleanExpression}}
                    var isValidResult = IsValidProposition.Evaluate(text);
                    return isValidResult.Satisfied;
                }
            }

            {{PropositionClass}}
            """;

        await VerifyAsync(source, expectedTransformedCode, line: 9, column: 16);
    }

    [Fact]
    public async Task Should_keep_guard_statement_before_converted_return_with_debug_tap()
    {
        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(string text)
                {
                    System.ArgumentNullException.ThrowIfNull(text);

                    return {{BooleanExpression}};
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
                private static readonly SpecBase<string, string> IsValidProposition = new IsValidProposition()
                    .Tap((model, result) =>
                        Debug.WriteLine($"[Motiv] IsValidProposition | Model: {model} | Satisfied: {result.Satisfied} | Reason: {result.Reason}"));

                public bool IsValid(string text)
                {
                    System.ArgumentNullException.ThrowIfNull(text);

                    // {{BooleanExpression}}
                    var isValidResult = IsValidProposition.Evaluate(text);
                    return isValidResult.Satisfied;
                }
            }

            {{PropositionClass}}
            """;

        await VerifyAsync(source, expectedTransformedCode, line: 9, column: 16,
            equivalenceKey: "ConvertToSpecWithDebugOutput");
    }

    [Fact]
    public async Task Should_keep_local_declaration_before_converted_return()
    {
        const string source =
          """
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(string input)
                {
                    var text = input.Trim();
                    return text.Length > 0;
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly IsValidProposition IsValidProposition = new();

                public bool IsValid(string input)
                {
                    var text = input.Trim();
                    // {{BooleanExpression}}
                    var isValidResult = IsValidProposition.Evaluate(text);
                    return isValidResult.Satisfied;
                }
            }

            {{PropositionClass}}
            """;

        await VerifyAsync(source, expectedTransformedCode, line: 8, column: 16);
    }

    [Fact]
    public async Task Should_keep_unreachable_statements_after_converted_return()
    {
        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(string text)
                {
                    return {{BooleanExpression}};
                    System.Console.WriteLine(text);
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly IsValidProposition IsValidProposition = new();

                public bool IsValid(string text)
                {
                    // {{BooleanExpression}}
                    var isValidResult = IsValidProposition.Evaluate(text);
                    return isValidResult.Satisfied;
                    System.Console.WriteLine(text);
                }
            }

            {{PropositionClass}}
            """;

        await VerifyAsync(source, expectedTransformedCode, line: 7, column: 16);
    }

    [Fact]
    public async Task Should_keep_other_returns_when_converting_final_return()
    {
        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(string text, bool skip)
                {
                    if (skip)
                    {
                        return false;
                    }

                    return {{BooleanExpression}};
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly IsValidProposition IsValidProposition = new();

                public bool IsValid(string text, bool skip)
                {
                    if (skip)
                    {
                        return false;
                    }

                    // {{BooleanExpression}}
                    var isValidResult = IsValidProposition.Evaluate(text);
                    return isValidResult.Satisfied;
                }
            }

            {{PropositionClass}}
            """;

        await VerifyAsync(source, expectedTransformedCode, line: 12, column: 16);
    }

    [Fact]
    public async Task Should_keep_other_returns_when_converting_nested_return()
    {
        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(string text, bool check)
                {
                    if (check)
                    {
                        return {{BooleanExpression}};
                    }

                    return true;
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly IsValidProposition IsValidProposition = new();

                public bool IsValid(string text, bool check)
                {
                    if (check)
                    {
                        // {{BooleanExpression}}
                        var isValidResult = IsValidProposition.Evaluate(text);
                        return isValidResult.Satisfied;
                    }

                    return true;
                }
            }

            {{PropositionClass}}
            """;

        await VerifyAsync(source, expectedTransformedCode, line: 9, column: 20);
    }

    [Fact]
    public async Task Should_convert_embedded_return_in_place()
    {
        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(string text, bool check)
                {
                    if (check)
                        return {{BooleanExpression}};

                    return true;
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly IsValidProposition IsValidProposition = new();

                public bool IsValid(string text, bool check)
                {
                    if (check)
                        return IsValidProposition.Matches(text);

                    return true;
                }
            }

            {{PropositionClass}}
            """;

        await VerifyAsync(source, expectedTransformedCode, line: 8, column: 20);
    }

    [Fact]
    public async Task Should_keep_statements_around_converted_local_declaration()
    {
        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(string text)
                {
                    System.ArgumentNullException.ThrowIfNull(text);
                    var isValid = {{BooleanExpression}};
                    System.Console.WriteLine(isValid);
                    return isValid;
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly IsValidProposition IsValidProposition = new();

                public bool IsValid(string text)
                {
                    System.ArgumentNullException.ThrowIfNull(text);
                    // {{BooleanExpression}}
                    var isValidResult = IsValidProposition.Evaluate(text);
                    var isValid = isValidResult.Satisfied;
                    System.Console.WriteLine(isValid);
                    return isValid;
                }
            }

            {{PropositionClass}}
            """;

        await VerifyAsync(source, expectedTransformedCode, line: 8, column: 23);
    }

    private static async Task VerifyAsync(
        string source,
        string expectedTransformedCode,
        int line,
        int column,
        string equivalenceKey = "ConvertToSpec")
    {
        var test = new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            CodeActionEquivalenceKey = equivalenceKey,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, line, column, line, column + BooleanExpression.Length)
            }
        };

        await test.RunAsync();
    }
}
