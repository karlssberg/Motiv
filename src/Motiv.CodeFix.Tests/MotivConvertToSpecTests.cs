using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Motiv.CodeFix.Tests.CSharpCodeFixVerifier<Motiv.Analyzer.MotivAnalyzer, Motiv.CodeFix.MotivCodeFixProvider>;

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
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }
    [Fact]
    public async Task Should_convert_multiple_variable_boolean_return_expressions_that_can_be_converted_to_spec()
    {
        const string clause1 = "valueA > valueB";
        const string clause2 = "valueC";
        const string booleanExpression = $"{clause1} && {clause2}";

        const string source =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(int valueA, int valueB, bool valueC) => {{booleanExpression}};
              }
              """;

        const string expectedTransformedCode =
          $$"""
            using System.Diagnostics;
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private readonly Proposition _proposition = new Proposition();
                public bool IsValid(int valueA, int valueB, bool valueC)
                {
                    // {{booleanExpression}}
                    var result = _proposition.IsSatisfiedBy(new Proposition.Model(valueA, valueB, valueC));
                    Debug.WriteLine(result.Reason);
                    return result.Satisfied;
                }
            }

            public class Proposition() : Spec<Proposition.Model>(
                Clause1.AndAlso(Clause2))
            {
                public record Model(int ValueA, int ValueB, bool ValueC);

                private static readonly SpecBase<Model> Clause1 =
                    Spec.Build((Model m) => m.ValueA > m.ValueB)
                        .WhenTrue("{{clause1}} == true")
                        .WhenFalse("{{clause1}} == false")
                        .Create();

                private static readonly SpecBase<Model> Clause2 =
                    Spec.Build((Model m) => m.ValueC)
                        .WhenTrue("{{clause2}} == true")
                        .WhenFalse("{{clause2}} == false")
                        .Create();
            }

            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                // This should expect a diagnostic when a boolean method is found that could be converted to a spec
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 5, 65, 5, 65 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_multiple_variable_boolean_expression_statement_that_can_be_converted_to_spec()
    {
        const string clause1 = "valueA > valueB";
        const string clause2 = "valueC";
        const string booleanExpression = $"{clause1} && {clause2}";

        const string source =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public void IsValid(int valueA, int valueB, bool valueC)
                  {
                      var isSatisfied = {{booleanExpression}};
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
                private readonly Proposition _proposition = new Proposition();
                public void IsValid(int valueA, int valueB, bool valueC)
                {
                    // {{booleanExpression}}
                    var result = _proposition.IsSatisfiedBy(new Proposition.Model(valueA, valueB, valueC));
                    Debug.WriteLine(result.Reason);
                    var isSatisfied = result.Satisfied;
                }
            }

            public class Proposition() : Spec<Proposition.Model>(
                Clause1.AndAlso(Clause2))
            {
                public record Model(int ValueA, int ValueB, bool ValueC);

                private static readonly SpecBase<Model> Clause1 =
                    Spec.Build((Model m) => m.ValueA > m.ValueB)
                        .WhenTrue("{{clause1}} == true")
                        .WhenFalse("{{clause1}} == false")
                        .Create();

                private static readonly SpecBase<Model> Clause2 =
                    Spec.Build((Model m) => m.ValueC)
                        .WhenTrue("{{clause2}} == true")
                        .WhenFalse("{{clause2}} == false")
                        .Create();
            }

            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                // This should expect a diagnostic when a boolean method is found that could be converted to a spec
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 27, 7, 27 + booleanExpression.Length)
            }
        }.RunAsync();
    }

}
