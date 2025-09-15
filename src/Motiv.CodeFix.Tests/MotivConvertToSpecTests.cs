using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Motiv.CodeFix.Tests.CSharpCodeFixVerifier<Motiv.CodeFix.MotivAnalyzer, Motiv.CodeFix.MotivCodeFixProvider>;

namespace Motiv.CodeFix.Tests;

public class MotivConvertToSpecTests
{
    private const string Source = "Source.cs";


    [Fact]
    public async Task Should_convert_single_variable_boolean_expressions_that_can_be_converted_to_spec()
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
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(int value)
                {
                    return new Proposition().IsSatisfiedBy(value).Satisfied;
                }
            }

            public class Proposition() : Spec<int>(() =>
                Spec.Build((int value) => {{booleanExpression}})
                    .WhenTrue("({{booleanExpression}}) == true")
                    .WhenFalse("({{booleanExpression}}) == false")
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
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_multiple_variable_boolean_expressions_that_can_be_converted_to_spec()
    {
        const string booleanExpression = "valueA > valueB && valueC";

        const string source =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(int valueA, int valueB, bool valueC)
                  {
                      return {{booleanExpression}};
                  }
              }
              """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(int valueA, int valueB, bool valueC)
                {
                    return new Proposition().IsSatisfiedBy(new Model(valueA, valueB, valueC)).Satisfied;
                }
            }

            public class Proposition() : Spec<Model>(() =>
                Spec.Build((Model model) => model.ValueA > model.ValueB && model.ValueC)
                    .WhenTrue("({{booleanExpression}}) == true")
                    .WhenFalse("({{booleanExpression}}) == false")
                    .Create());

            public class Model
            {
                public Model(int valueA, int valueB, bool valueC)
                {
                    ValueA = valueA;
                    ValueB = valueB;
                    ValueC = valueC;
                }

                public int ValueA { get; }
                public int ValueB { get; }
                public bool ValueC { get; }
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                // This should expect a diagnostic when a boolean method is found that could be converted to a spec
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }
}
