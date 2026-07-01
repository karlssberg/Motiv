using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Motiv.Analyzer.Tests.CSharpDiagnosticAnalyzerVerifier<Motiv.Analyzer.NegatedSpecResultAnalyzer>;
using MotivAnalyzerCS = Motiv.Analyzer.Tests.CSharpDiagnosticAnalyzerVerifier<Motiv.Analyzer.MotivAnalyzer>;

namespace Motiv.Analyzer.Tests;

public class NegatedSpecResultAnalyzerTests
{
    private const string Source = "Source.cs";

    [Fact]
    public async Task Should_report_negated_spec_evaluation_result()
    {
        const string negatedExpression = "!IsPositive.Evaluate(value).Satisfied";
        const string code =
            $$"""
              using Motiv;

              namespace MyNamespace;

              public class MyClass
              {
                  private static readonly SpecBase<int, string> IsPositive =
                      Spec.Build((int value) => value > 0)
                          .Create("is positive");

                  public bool IsInvalid(int value)
                  {
                      return {{negatedExpression}};
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
                    new DiagnosticResult("MOTIV0007", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 13, 16, 13, 16 + negatedExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_negated_spec_evaluation_result_in_if_condition()
    {
        const string negatedExpression = "!IsPositive.Evaluate(value).Satisfied";
        const string code =
            $$"""
              using Motiv;

              namespace MyNamespace;

              public class MyClass
              {
                  private static readonly SpecBase<int, string> IsPositive =
                      Spec.Build((int value) => value > 0)
                          .Create("is positive");

                  public void Process(int value)
                  {
                      if ({{negatedExpression}})
                      {
                          return;
                      }
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
                    new DiagnosticResult("MOTIV0007", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 13, 13, 13, 13 + negatedExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_negated_legacy_IsSatisfiedBy_result()
    {
        const string negatedExpression = "!IsPositive.IsSatisfiedBy(value).Satisfied";
        const string code =
            $$"""
              using Motiv;

              namespace MyNamespace;

              public class MyClass
              {
                  private static readonly SpecBase<int, string> IsPositive =
                      Spec.Build((int value) => value > 0)
                          .Create("is positive");

                  public bool IsInvalid(int value)
                  {
                      return {{negatedExpression}};
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
                    new DiagnosticResult("MOTIV0007", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 13, 16, 13, 16 + negatedExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_negation_of_parenthesized_spec_evaluation_result()
    {
        const string negatedExpression = "!(IsPositive.Evaluate(value).Satisfied)";
        const string code =
            $$"""
              using Motiv;

              namespace MyNamespace;

              public class MyClass
              {
                  private static readonly SpecBase<int, string> IsPositive =
                      Spec.Build((int value) => value > 0)
                          .Create("is positive");

                  public bool IsInvalid(int value)
                  {
                      return {{negatedExpression}};
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
                    new DiagnosticResult("MOTIV0007", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 13, 16, 13, 16 + negatedExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_negation_when_evaluation_invocation_is_parenthesized()
    {
        const string negatedExpression = "!(IsPositive.Evaluate(value)).Satisfied";
        const string code =
            $$"""
              using Motiv;

              namespace MyNamespace;

              public class MyClass
              {
                  private static readonly SpecBase<int, string> IsPositive =
                      Spec.Build((int value) => value > 0)
                          .Create("is positive");

                  public bool IsInvalid(int value)
                  {
                      return {{negatedExpression}};
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
                    new DiagnosticResult("MOTIV0007", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 13, 16, 13, 16 + negatedExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_negation_of_structurally_similar_non_Motiv_type()
    {
        const string code =
            """
            namespace MyNamespace;

            public class FakeResult
            {
                public bool Satisfied { get; set; }
            }

            public class FakeSpec
            {
                public FakeResult Evaluate(int value) => new();
            }

            public class MyClass
            {
                private static readonly FakeSpec IsPositive = new();

                public bool IsInvalid(int value)
                {
                    return !IsPositive.Evaluate(value).Satisfied;
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, code) } },
            ExpectedDiagnostics = { } // Empty - not a Motiv spec evaluation
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_negation_of_plain_boolean()
    {
        const string code =
            """
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsInvalid(bool flag)
                {
                    return !flag;
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, code) } },
            ExpectedDiagnostics = { } // Empty - plain negation belongs to MOTIV0001
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_when_negation_is_nested_inside_larger_logical_expression()
    {
        const string code =
            """
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly SpecBase<int, string> IsPositive =
                    Spec.Build((int value) => value > 0)
                        .Create("is positive");

                public bool IsInvalid(int value, bool flag)
                {
                    return flag && !IsPositive.Evaluate(value).Satisfied;
                }
            }
            """;

        // The root logical expression is MOTIV0001 territory; MOTIV0007 stays quiet on nested negations
        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, code) } },
            ExpectedDiagnostics = { }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_not_report_MOTIV0001_on_negated_spec_evaluation_result()
    {
        const string code =
            """
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly SpecBase<int, string> IsPositive =
                    Spec.Build((int value) => value > 0)
                        .Create("is positive");

                public bool IsInvalid(int value)
                {
                    return !IsPositive.Evaluate(value).Satisfied;
                }
            }
            """;

        // MOTIV0007 owns this pattern - MOTIV0001 must not double-report it
        await new MotivAnalyzerCS.Test
        {
            TestState = { Sources = { (Source, code) } },
            ExpectedDiagnostics = { }
        }.RunAsync();
    }
}
