using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Motiv.CodeFix.Tests.CSharpCodeFixVerifier<Motiv.Analyzer.MotivAnalyzer, Motiv.CodeFix.MotivCodeFixProvider>;

namespace Motiv.CodeFix.Tests;

public class MotivConvertToSpecTests
{
    private const string Source = "Source.cs";
    [Fact]
    public async Task Should_convert_boolean_expressions_that_can_be_converted_to_spec()
    {
        const string booleanExpression = "value > 0";

        const string source =
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

        const string expectedTransformedCode =
            """
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly IsValidSpec IsValidSpec = new();

                public bool IsValid(int value)
                {
                    return IsValidSpec.IsSatisfiedBy(value).Satisfied;
                }
            }

            public class IsValidSpec() : Spec<int>(() =>
                Spec.Build((int value) => value > 0)
                    .WhenTrue("value is greater than 0")
                    .WhenFalse("value is not greater than 0")
                    .Create());
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                // This should expect a diagnostic when a boolean method is found that could be converted to a spec
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden)
                    .WithSpan(Source, 7, 16, 7, 25)
            }
        }.RunAsync();
    }
}
