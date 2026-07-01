using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Motiv.CodeFix.Tests.CSharpCodeFixVerifier<
        Motiv.Analyzer.NegatedSpecResultAnalyzer,
        Motiv.CodeFix.NegatedSpecResultCodeFixProvider>;

namespace Motiv.CodeFix.Tests;

public class NegatedSpecResultCodeFixTests
{
    private const string Source = "Source.cs";

    [Fact]
    public async Task Should_compose_negation_into_spec_when_result_is_inverted()
    {
        const string negatedExpression = "!IsPositive.Evaluate(value).Satisfied";

        const string source =
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

        const string expectedTransformedCode =
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
                    return IsPositive.Not().Evaluate(value).Satisfied;
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0007", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 13, 16, 13, 16 + negatedExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_compose_negation_into_spec_within_if_condition()
    {
        const string negatedExpression = "!IsPositive.Evaluate(value).Satisfied";

        const string source =
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

        const string expectedTransformedCode =
            """
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private static readonly SpecBase<int, string> IsPositive =
                    Spec.Build((int value) => value > 0)
                        .Create("is positive");

                public void Process(int value)
                {
                    if (IsPositive.Not().Evaluate(value).Satisfied)
                    {
                        return;
                    }
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0007", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 13, 13, 13, 13 + negatedExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_compose_negation_when_operand_is_parenthesized()
    {
        const string negatedExpression = "!(IsPositive.Evaluate(value).Satisfied)";

        const string source =
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

        const string expectedTransformedCode =
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
                    return IsPositive.Not().Evaluate(value).Satisfied;
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0007", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 13, 16, 13, 16 + negatedExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_preserve_legacy_IsSatisfiedBy_invocation_when_composing_negation()
    {
        const string negatedExpression = "!IsPositive.IsSatisfiedBy(value).Satisfied";

        const string source =
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

        const string expectedTransformedCode =
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
                    return IsPositive.Not().IsSatisfiedBy(value).Satisfied;
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0007", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 13, 16, 13, 16 + negatedExpression.Length)
            }
        }.RunAsync();
    }
}
