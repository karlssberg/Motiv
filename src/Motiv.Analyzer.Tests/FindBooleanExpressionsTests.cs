using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Motiv.Analyzer.Tests.CSharpDiagnosticAnalyzerVerifier<Motiv.Analyzer.MotivAnalyzer>;

namespace Motiv.Analyzer.Tests;

public class FindBooleanExpressionsTests
{
    private const string Source = "Source.cs";

    [Fact]
    public async Task Should_analyze_and_identify_minimal_boolean_expressions_that_can_be_converted_to_specs()
    {
        // Diagnostic analyzer test
        const string booleanExpression = "value > 0";
        const string code =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(int value)
                {
                    return {{booleanExpression}};
                }
            }
            """;


        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (Source, code) },
                ExpectedDiagnostics =
                {
                    // This should expect a diagnostic when a boolean method is found that could be converted to a spec
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_analyze_and_identify_complex_boolean_expressions_that_can_be_converted_to_specs()
    {
        // Diagnostic analyzer test
        const string booleanExpression = "value > 0 && value < 100";
        const string code =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(int value)
                  {
                      return {{booleanExpression}};
                  }
              }
              """;


        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (Source, code) },
                ExpectedDiagnostics =
                {
                    // This should expect a diagnostic when a boolean method is found that could be converted to a spec
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }
}
