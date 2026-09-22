using Microsoft.CodeAnalysis.Testing;
using VerifyCS = Motiv.Analyzer.Tests.CSharpDiagnosticAnalyzerVerifier<Motiv.Analyzer.MotivAnalyzer>;

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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_ignore_boolean_expressions_inside_spec_fluent_builder()
    {
        const string source =
            """
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                public static readonly SpecBase<int, string> IsPositive =
                    Spec.Build((int value) => value > 0)
                        .WhenTrue("Value is positive")
                        .WhenFalse("Value is not positive")
                        .Create();
            }
            """;

        // No diagnostics should be reported since the expression is inside Spec.Build()
        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            ExpectedDiagnostics = { } // Empty - no diagnostics expected
        }.RunAsync();
    }

    [Fact]
    public async Task Should_ignore_boolean_expressions_deeply_nested_inside_spec_fluent_builder()
    {
        const string source =
            """
            using System;
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                public static readonly SpecBase<int, string> IsPositive =
                    Spec.Build((int value) =>
                         {
                             Func<bool> nested = () => value > 0;
                             return nested();
                         })
                        .WhenTrue("Value is positive")
                        .WhenFalse("Value is not positive")
                        .Create();
            }
            """;

        // No diagnostics should be reported since the expression is inside Spec.Build()
        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            ExpectedDiagnostics = { } // Empty - no diagnostics expected
        }.RunAsync();
    }

    [Fact]
    public async Task Should_create_a_single_diagnostic_for_the_root_boolean_expression_with_sub_expressions()
    {
        const string clause1 = "valueA >= 0";
        const string clause2 = "valueB >= 0";
        const string clause3 = "valueC >= 1";
        const string clause4 = "valueC <= 10";
        const string booleanExpression = $"{clause1} && ({clause2} || !({clause3}^{clause4}))";

        const string source =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public void IsValid(int valueA, int valueB, int valueC)
                  {
                      var isSatisfied = {{booleanExpression}};
                  }
              }
              """;

        // Only ONE diagnostic should be reported for the root expression, not for nested sub-expressions
        await new VerifyCS.Test
        {
            TestState =
            {
                Sources = { (Source, source) },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 27, 7, 27 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_detect_is_type_check_expression()
    {
        const string booleanExpression = "obj is string";
        const string code =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(object obj)
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_detect_is_type_check_with_declaration_pattern()
    {
        const string booleanExpression = "obj is string s";
        const string code =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(object obj)
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_detect_negated_is_type_check()
    {
        const string booleanExpression = "obj is not string";
        const string code =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(object obj)
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_detect_property_pattern_expression()
    {
        const string booleanExpression = "obj is { Length: > 0 }";
        const string code =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(string obj)
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_detect_relational_pattern_expression()
    {
        const string booleanExpression = "value is > 5";
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_detect_logical_and_pattern_expression()
    {
        const string booleanExpression = "value is > 5 and < 10";
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_detect_logical_or_pattern_expression()
    {
        const string booleanExpression = "value is 1 or 2 or 3";
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_single_diagnostic_when_is_pattern_is_right_operand_of_logical_and()
    {
        const string booleanExpression = "value > 0 && obj is string";
        const string code =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(int value, object obj)
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_single_diagnostic_when_is_pattern_is_left_operand_of_logical_and()
    {
        const string booleanExpression = "obj is string && value > 0";
        const string code =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(int value, object obj)
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_single_diagnostic_when_is_pattern_is_right_operand_of_logical_or()
    {
        const string booleanExpression = "value > 0 || obj is string";
        const string code =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(int value, object obj)
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_single_diagnostic_when_parenthesized_is_pattern_is_in_binary_expression()
    {
        const string booleanExpression = "value > 0 && (obj is string)";
        const string code =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(int value, object obj)
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_single_diagnostic_when_binary_expression_is_nested_in_property_pattern()
    {
        const string booleanExpression = "obj is { Length: > 0 }";
        const string code =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(string obj)
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_report_single_diagnostic_for_complex_pattern_with_relational_subpatterns()
    {
        const string code =
            """
            namespace MyNamespace;

            public class Container { public int Value { get; set; } }

            public class MyClass
            {
                public bool IsValid(Container obj)
                {
                    return obj is { Value: > 5 and < 10 };
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
                    new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                        .WithSpan(Source, 9, 16, 9, 46)
                }
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_ignore_is_type_check_inside_spec_fluent_builder()
    {
        const string source =
            """
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                public static readonly SpecBase<object, string> IsString =
                    Spec.Build((object x) => x is string)
                        .WhenTrue("is string")
                        .WhenFalse("not string")
                        .Create();
            }
            """;

        // No diagnostics should be reported since the expression is inside Spec.Build()
        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            ExpectedDiagnostics = { } // Empty - no diagnostics expected
        }.RunAsync();
    }

    [Fact]
    public async Task Should_ignore_property_pattern_inside_spec_fluent_builder()
    {
        const string source =
            """
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                public static readonly SpecBase<string, string> HasLength =
                    Spec.Build((string x) => x is { Length: > 0 })
                        .WhenTrue("has length")
                        .WhenFalse("empty")
                        .Create();
            }
            """;

        // No diagnostics should be reported since the expression is inside Spec.Build()
        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            ExpectedDiagnostics = { } // Empty - no diagnostics expected
        }.RunAsync();
    }
}
