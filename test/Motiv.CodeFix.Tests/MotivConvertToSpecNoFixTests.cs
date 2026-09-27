using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Motiv.CodeFix.Tests.CSharpCodeFixVerifier<Motiv.Analyzer.MotivAnalyzer, Motiv.CodeFix.MotivCodeFixProvider>;

namespace Motiv.CodeFix.Tests;

/// <summary>
///     Positions where the expression is flagged but no fix can hold it, so none is offered and the document is
///     left as it was.
/// </summary>
public class MotivConvertToSpecNoFixTests
{
    private const string Source = "Source.cs";

    [Theory]
    [InlineData("struct")]
    [InlineData("record struct")]
    [InlineData("interface")]
    public async Task Should_not_offer_a_fix_when_an_expression_calls_an_instance_method_of_a_type_other_than_a_class(
        string typeKeyword)
    {
        const string booleanExpression = "value > 0 && IsKnown(value)";

        var source =
          $$"""
            namespace MyNamespace;

            public {{typeKeyword}} Values
            {
                public bool IsValid(int value) => {{booleanExpression}};

                bool IsKnown(int value) => value.Equals(42);
            }
            """;

        await VerifyNoFix(source, 5, 39, booleanExpression);
    }

    [Fact]
    public async Task Should_not_offer_a_fix_when_an_expression_calls_an_instance_method_of_a_positional_record()
    {
        const string booleanExpression = "value > 0 && IsKnown(value)";

        const string source =
          $$"""
            namespace MyNamespace;

            public record Values(int Limit)
            {
                public bool IsValid(int value) => {{booleanExpression}};

                bool IsKnown(int value) => value.Equals(Limit);
            }
            """;

        await VerifyNoFix(source, 5, 39, booleanExpression);
    }

    [Fact]
    public async Task Should_not_offer_a_fix_when_expression_is_a_primary_constructor_base_argument()
    {
        const string booleanExpression = "n > 0 && n < 10";

        const string source =
          $$"""
            namespace MyNamespace;

            public class Base(bool isValid);

            public class Derived(int n) : Base({{booleanExpression}});
            """;

        await VerifyNoFix(source, 5, 36, booleanExpression);
    }

    private static async Task VerifyNoFix(string source, int line, int column, string booleanExpression) =>
        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, source) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, line, column, line, column + booleanExpression.Length)
            }
        }.RunAsync();
}
