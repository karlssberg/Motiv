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
                    return new IsValidProposition().IsSatisfiedBy(value).Satisfied;
                }
            }

            public class IsValidProposition() : Spec<int>(() =>
                Spec.Build((int value) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
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
    public async Task Should_convert_double_variable_boolean_return_expressions_that_can_be_converted_to_spec()
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
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private readonly IsValidProposition _isValidProposition = new IsValidProposition();
                public bool IsValid(int valueA, int valueB, bool valueC)
                {
                    // {{booleanExpression}}
                    var result = _isValidProposition.IsSatisfiedBy(new IsValidProposition.Model(valueA, valueB, valueC));
                    return result.Satisfied;
                }
            }

            public class IsValidProposition() : Spec<IsValidProposition.Model>(() =>
            {
                var clause1 = Spec
                    .Build((Model m) => m.ValueA > m.ValueB)
                    .Create("{{clause1}}");

                var isValueC = Spec
                    .Build((Model m) => m.ValueC)
                    .Create("{{clause2}}");

                return clause1.AndAlso(isValueC);
            })
            {
                public record Model(int ValueA, int ValueB, bool ValueC);
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
    public async Task Should_convert_double_variable_boolean_expression_statement_that_can_be_converted_to_spec()
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
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private readonly IsSatisfiedProposition _isSatisfiedProposition = new IsSatisfiedProposition();
                public void IsValid(int valueA, int valueB, bool valueC)
                {
                    // {{booleanExpression}}
                    var result = _isSatisfiedProposition.IsSatisfiedBy(new IsSatisfiedProposition.Model(valueA, valueB, valueC));
                    var isSatisfied = result.Satisfied;
                }
            }

            public class IsSatisfiedProposition() : Spec<IsSatisfiedProposition.Model>(() =>
            {
                var clause1 = Spec
                    .Build((Model m) => m.ValueA > m.ValueB)
                    .Create("{{clause1}}");

                var isValueC = Spec
                    .Build((Model m) => m.ValueC)
                    .Create("{{clause2}}");

                return clause1.AndAlso(isValueC);
            })
            {
                public record Model(int ValueA, int ValueB, bool ValueC);
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

    [Fact]
    public async Task Should_convert_many_nested_clauses_into_a_spec()
    {
        const string clause1 = "valueA >= 0";
        const string clause2 = "valueB >= 0";
        const string clause3 = "valueC >= 1";
        const string clause4 = "valueC <= 10";

        const string source =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public void IsValid(int valueA, int valueB, int valueC)
                  {
                      var isSatisfied = {{clause1}} && ({{clause2}} || !({{clause3}} ^ {{clause4}}));
                  }
              }
              """;

        const string expectedTransformedCode =
            $$"""
              using Motiv;

              namespace MyNamespace;

              public class MyClass
              {
                  private readonly IsSatisfiedProposition _isSatisfiedProposition = new IsSatisfiedProposition();
                  public void IsValid(int valueA, int valueB, int valueC)
                  {
                      // {{clause1}} && ({{clause2}} ||
                      //     !({{clause3}} ^ {{clause4}}))
                      var result = _isSatisfiedProposition.IsSatisfiedBy(new IsSatisfiedProposition.Model(valueA, valueB, valueC));
                      var isSatisfied = result.Satisfied;
                  }
              }

              public class IsSatisfiedProposition() : Spec<IsSatisfiedProposition.Model>(() =>
              {
                  var isValueANonNegative = Spec
                      .Build((Model m) => m.ValueA >= 0)
                      .Create("{{clause1}}");

                  var isValueBNonNegative = Spec
                      .Build((Model m) => m.ValueB >= 0)
                      .Create("{{clause2}}");

                  var isValueCAtLeast1 = Spec
                      .Build((Model m) => m.ValueC >= 1)
                      .Create("{{clause3}}");

                  var isValueCAtMost10 = Spec
                      .Build((Model m) => m.ValueC <= 10)
                      .Create("{{clause4}}");

                  return isValueANonNegative.AndAlso((isValueBNonNegative.OrElse(!(isValueCAtLeast1 ^ isValueCAtMost10))));
              })
              {
                  public record Model(int ValueA, int ValueB, int ValueC);
              }
              """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 27, 7, 27 + $"{clause1} && ({clause2} || !({clause3} ^ {clause4}))".Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_derive_name_from_member_access_root()
    {
        const string booleanExpression = "order.Total > 100";

        const string source =
          $$"""
            namespace MyNamespace;

            public class Order
            {
                public int Total { get; set; }
            }

            public class MyClass
            {
                public bool IsValid(Order order)
                {
                    return {{booleanExpression}};
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class Order
            {
                public int Total { get; set; }
            }

            public class MyClass
            {
                public bool IsValid(Order order)
                {
                    return new IsValidProposition().IsSatisfiedBy(order).Satisfied;
                }
            }

            public class IsValidProposition() : Spec<MyNamespace.Order>(() =>
                Spec.Build((MyNamespace.Order order) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 12, 16, 12, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_derive_common_root_from_multiple_member_accesses()
    {
        const string booleanExpression = "order.Total > 100 && order.IsActive";

        const string source =
          $$"""
            namespace MyNamespace;

            public class Order
            {
                public int Total { get; set; }
                public bool IsActive { get; set; }
            }

            public class MyClass
            {
                public bool IsValid(Order order)
                {
                    return {{booleanExpression}};
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class Order
            {
                public int Total { get; set; }
                public bool IsActive { get; set; }
            }

            public class MyClass
            {
                public bool IsValid(Order order)
                {
                    return new IsValidProposition().IsSatisfiedBy(order).Satisfied;
                }
            }

            public class IsValidProposition() : Spec<MyNamespace.Order>(() =>
                Spec.Build((MyNamespace.Order order) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 13, 16, 13, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_derive_name_from_is_expression_variable()
    {
        const string booleanExpression = "obj is string";

        const string source =
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

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(object obj)
                {
                    return new IsValidProposition().IsSatisfiedBy(obj).Satisfied;
                }
            }

            public class IsValidProposition() : Spec<object>(() =>
                Spec.Build((object obj) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_use_fallback_name_for_unrelated_variables()
    {
        const string clause1 = "x > 5";
        const string clause2 = "y < 10";
        const string booleanExpression = $"{clause1} && {clause2}";

        const string source =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsValid(int x, int y) => {{booleanExpression}};
              }
              """;

        const string expectedTransformedCode =
            $$"""
              using Motiv;

              namespace MyNamespace;

              public class MyClass
              {
                  private readonly IsValidProposition _isValidProposition = new IsValidProposition();
                  public bool IsValid(int x, int y)
                  {
                      // {{booleanExpression}}
                      var result = _isValidProposition.IsSatisfiedBy(new IsValidProposition.Model(x, y));
                      return result.Satisfied;
                  }
              }

              public class IsValidProposition() : Spec<IsValidProposition.Model>(() =>
              {
                  var isXGreaterThan5 = Spec
                      .Build((Model m) => m.X > 5)
                      .Create("{{clause1}}");

                  var isYLessThan10 = Spec
                      .Build((Model m) => m.Y < 10)
                      .Create("{{clause2}}");

                  return isXGreaterThan5.AndAlso(isYLessThan10);
              })
              {
                  public record Model(int X, int Y);
              }
              """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 5, 42, 5, 42 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_deduplicate_identical_clauses_to_avoid_compile_errors()
    {
        const string clause1 = "age > 0";
        const string clause2 = "name";
        const string booleanExpression = $"{clause1} && {clause2} && {clause1}";

        const string source =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public void IsValid(int age, bool name)
                  {
                      var isSatisfied = {{booleanExpression}};
                  }
              }
              """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private readonly IsSatisfiedProposition _isSatisfiedProposition = new IsSatisfiedProposition();
                public void IsValid(int age, bool name)
                {
                    // {{booleanExpression}}
                    var result = _isSatisfiedProposition.IsSatisfiedBy(new IsSatisfiedProposition.Model(age, name));
                    var isSatisfied = result.Satisfied;
                }
            }

            public class IsSatisfiedProposition() : Spec<IsSatisfiedProposition.Model>(() =>
            {
                var isAgePositive = Spec
                    .Build((Model m) => m.Age > 0)
                    .Create("{{clause1}}");

                var isName = Spec
                    .Build((Model m) => m.Name)
                    .Create("{{clause2}}");

                return isAgePositive.AndAlso(isName)
                    .AndAlso(isAgePositive);
            })
            {
                public record Model(int Age, bool Name);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 27, 7, 27 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_deduplicate_clauses_in_or_expressions_with_shared_subclauses()
    {
        const string clause1 = "valueA >= 0";
        const string clause2 = "valueC >= 1";
        const string clause3 = "valueB >= 0";

        const string source =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public void IsValid(int valueA, int valueB, int valueC)
                  {
                      var isSatisfied = ({{clause1}} && {{clause2}}) || ({{clause3}} && {{clause2}});
                  }
              }
              """;

        const string expectedTransformedCode =
            $$"""
              using Motiv;

              namespace MyNamespace;

              public class MyClass
              {
                  private readonly IsSatisfiedProposition _isSatisfiedProposition = new IsSatisfiedProposition();
                  public void IsValid(int valueA, int valueB, int valueC)
                  {
                      // ({{clause1}} && {{clause2}}) ||
                      //     ({{clause3}} && {{clause2}})
                      var result = _isSatisfiedProposition.IsSatisfiedBy(new IsSatisfiedProposition.Model(valueA, valueC, valueB));
                      var isSatisfied = result.Satisfied;
                  }
              }

              public class IsSatisfiedProposition() : Spec<IsSatisfiedProposition.Model>(() =>
              {
                  var isValueANonNegative = Spec
                      .Build((Model m) => m.ValueA >= 0)
                      .Create("{{clause1}}");

                  var isValueCAtLeast1 = Spec
                      .Build((Model m) => m.ValueC >= 1)
                      .Create("{{clause2}}");

                  var isValueBNonNegative = Spec
                      .Build((Model m) => m.ValueB >= 0)
                      .Create("{{clause3}}");

                  return (isValueANonNegative.AndAlso(isValueCAtLeast1)).OrElse((isValueBNonNegative.AndAlso(isValueCAtLeast1)));
              })
              {
                  public record Model(int ValueA, int ValueC, int ValueB);
              }
              """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 27, 7, 27 + $"({clause1} && {clause2}) || ({clause3} && {clause2})".Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_deduplicate_clauses_with_less_than_comparison()
    {
        const string clause1 = "valueA >= 0";
        const string clause2 = "1 < valueC";
        const string clause3 = "valueB >= 0";

        const string source =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsFeatureEnabled(int valueA, int valueB, int valueC)
                  {
                      return ({{clause1}} && {{clause2}}) || ({{clause3}} && {{clause2}});
                  }
              }
              """;

        const string expectedTransformedCode =
            $$"""
              using Motiv;

              namespace MyNamespace;

              public class MyClass
              {
                  private readonly IsFeatureEnabledProposition _isFeatureEnabledProposition = new IsFeatureEnabledProposition();
                  public bool IsFeatureEnabled(int valueA, int valueB, int valueC)
                  {
                      // ({{clause1}} && {{clause2}}) ||
                      //     ({{clause3}} && {{clause2}})
                      var result = _isFeatureEnabledProposition.IsSatisfiedBy(new IsFeatureEnabledProposition.Model(valueA, valueC, valueB));
                      return result.Satisfied;
                  }
              }

              public class IsFeatureEnabledProposition() : Spec<IsFeatureEnabledProposition.Model>(() =>
              {
                  var isValueANonNegative = Spec
                      .Build((Model m) => m.ValueA >= 0)
                      .Create("{{clause1}}");

                  var is1LessThanValueC = Spec
                      .Build((Model m) => 1 < m.ValueC)
                      .Create("{{clause2}}");

                  var isValueBNonNegative = Spec
                      .Build((Model m) => m.ValueB >= 0)
                      .Create("{{clause3}}");

                  return (isValueANonNegative.AndAlso(is1LessThanValueC)).OrElse((isValueBNonNegative.AndAlso(is1LessThanValueC)));
              })
              {
                  public record Model(int ValueA, int ValueC, int ValueB);
              }
              """;

          await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + $"({clause1} && {clause2}) || ({clause3} && {clause2})".Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_escape_double_quotes_in_string_literals_within_WhenTrue_and_WhenFalse()
    {
        const string booleanExpression = "text == \"green\"";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsGreen(string text)
                {
                    return {{booleanExpression}};
                }
            }
            """;

        const string escapedExpression = "text == \\\"green\\\"";
        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                public bool IsGreen(string text)
                {
                    return new IsGreenProposition().IsSatisfiedBy(text).Satisfied;
                }
            }

            public class IsGreenProposition() : Spec<string>(() =>
                Spec.Build((string text) => {{booleanExpression}})
                    .Create("{{escapedExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_inject_instance_via_constructor_when_expression_calls_instance_methods()
    {
        const string clause1 = "string.IsNullOrEmpty(text)";
        const string clause2 = "IsGreen(text)";
        const string booleanExpression = $"{clause1} && {clause2}";

        const string source =
            $$"""
              namespace MyNamespace;

              public class Playground
              {
                  public bool IsFeatureEnabled(string text)
                  {
                      return {{booleanExpression}};
                  }

                  public bool IsGreen(string text)
                  {
                      return text == "green";
                  }
              }
              """;

        var expectedTransformedCode =
              $$"""
                using Motiv;

                namespace MyNamespace;

                public class Playground
                {
                    private readonly IsFeatureEnabledProposition _isFeatureEnabledProposition;
                    public Playground()
                    {
                        _isFeatureEnabledProposition = new IsFeatureEnabledProposition(this);
                    }

                    public bool IsFeatureEnabled(string text)
                    {
                        // {{booleanExpression}}
                        var result = _isFeatureEnabledProposition.IsSatisfiedBy(text);
                        return result.Satisfied;
                    }

                    public bool IsGreen(string text)
                    {
                        return new IsGreenProposition().IsSatisfiedBy(text).Satisfied;
                    }
                }

                public class IsFeatureEnabledProposition(MyNamespace.Playground instance) : Spec<string>(() =>
                {
                    var clause1 = Spec
                        .Build((string text) => {{clause1}})
                        .Create("{{clause1}}");

                    var clause2 = Spec
                        .Build((string text) => instance.IsGreen(text))
                        .Create("{{clause2}}");

                    return clause1.AndAlso(clause2);
                });

                public class IsGreenProposition() : Spec<string>(() =>
                    Spec.Build((string text) => text == "green")
                        .Create("text == \"green\""));
                """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                // The main expression we're testing
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length),
                // The IsGreen method is also detected as convertible
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 12, 16, 12, 31)
            },
            NumberOfFixAllIterations = 2
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_multiple_variables_with_instance_methods_and_generate_model()
    {
        const string clause1 = "valueA >= 0";
        const string clause2 = "1 < valueC";
        const string clause3 = "string.IsNullOrEmpty(text)";
        const string clause4 = "IsGreen(text)";
        const string booleanExpression = $"({clause1} && {clause2}) && ({clause3} && {clause4})";

        const string source =
            $$"""
              namespace MyNamespace;

              public class Playground
              {
                  public bool IsFeatureEnabled(int valueA, int valueC, string text)
                  {
                      return {{booleanExpression}};
                  }

                  public bool IsGreen(string text)
                  {
                      return text == "green";
                  }
              }
              """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class Playground
            {
                private readonly IsFeatureEnabledProposition _isFeatureEnabledProposition;
                public Playground()
                {
                    _isFeatureEnabledProposition = new IsFeatureEnabledProposition(this);
                }

                public bool IsFeatureEnabled(int valueA, int valueC, string text)
                {
                    // {{booleanExpression}}
                    var result = _isFeatureEnabledProposition.IsSatisfiedBy(new IsFeatureEnabledProposition.Model(valueA, valueC, text));
                    return result.Satisfied;
                }

                public bool IsGreen(string text)
                {
                    return new IsGreenProposition().IsSatisfiedBy(text).Satisfied;
                }
            }

            public class IsFeatureEnabledProposition(MyNamespace.Playground instance) : Spec<IsFeatureEnabledProposition.Model>(() =>
            {
                var isValueANonNegative = Spec
                    .Build((Model m) => m.ValueA >= 0)
                    .Create("{{clause1}}");

                var is1LessThanValueC = Spec
                    .Build((Model m) => 1 < m.ValueC)
                    .Create("{{clause2}}");

                var clause3 = Spec
                    .Build((Model m) => string.IsNullOrEmpty(m.Text))
                    .Create("{{clause3}}");

                var clause4 = Spec
                    .Build((Model m) => instance.IsGreen(m.Text))
                    .Create("{{clause4}}");

                return (isValueANonNegative.AndAlso(is1LessThanValueC)).AndAlso((clause3.AndAlso(clause4)));
            })
            {
                public record Model(int ValueA, int ValueC, string Text);
            }

            public class IsGreenProposition() : Spec<string>(() =>
                Spec.Build((string text) => text == "green")
                    .Create("text == \"green\""));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) }},
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length),
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 12, 16, 12, 31)
            },
            NumberOfFixAllIterations = 2
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_complex_inside_a_namespace_block_and_handle_multiple_clauses_with_instance_methods()
    {
        const string source =
          $$"""
            namespace MyNamespace
            {
                public class Playground()
                {
                    public bool IsFeatureEnabled(int valueA, int valueB, int valueC, string text) =>
                        (valueA >= 0 && 1 < valueC) ||
                               valueB >= 0 && 1 < valueC && string.IsNullOrEmpty(text) && IsGreen(text);
                }
            }
            """;

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;
            {
                public class Playground
                {
                    private readonly IsFeatureEnabledProposition _isFeatureEnabledProposition;
                    public Playground()
                    {
                        _isFeatureEnabledProposition = new IsFeatureEnabledProposition(this);
                    }

                    public bool IsFeatureEnabled(int valueA, int valueB, int valueC, string text)
                    {
                        // (valueA >= 0 && 1 < valueC) ||
                        //     valueB >= 0 && 1 < valueC && (string.IsNullOrEmpty(text) && IsGreen(text))
                        var result = _isFeatureEnabledProposition.IsSatisfiedBy(new IsFeatureEnabledProposition.Model(valueA, valueC, valueB, text));
                        return result.Satisfied;
                    }
                }

                public class IsFeatureEnabledProposition(Playground instance) : Spec<IsFeatureEnabledProposition.Model>(() =>
                {
                    var isValueANonNegative = Spec
                        .Build((Model m) => m.ValueA >= 0)
                        .Create("valueA >= 0");

                    var is1LessThanValueC = Spec
                        .Build((Model m) => 1 < m.ValueC)
                        .Create("1 < valueC");

                    var isValueBNonNegative = Spec
                        .Build((Model m) => m.ValueB >= 0)
                        .Create("valueB >= 0");

                    var clause4 = Spec
                        .Build((Model m) => string.IsNullOrEmpty(m.Text))
                        .Create("string.IsNullOrEmpty(text)");

                    var clause5 = Spec
                        .Build((Model m) => instance.IsGreen(m.Text))
                        .Create("IsGreen(text)");

                    return (isValueANonNegative.AndAlso(is1LessThanValueC)).OrElse(isValueBNonNegative.AndAlso(is1LessThanValueC)
                        .AndAlso((clause4.AndAlso(clause5))));
                })
                {
                    public record Model(int ValueA, int ValueC, int ValueB, string Text);
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState =
            {
                Sources = { (Source, expectedTransformedCode) },
                ExpectedDiagnostics =
                {
                    DiagnosticResult.CompilerError("CS1022").WithSpan(Source, 4, 1, 4, 2),
                    DiagnosticResult.CompilerError("CS1061").WithSpan(Source, 41, 42, 41, 49).WithArguments("MyNamespace.Playground", "IsGreen"),
                    DiagnosticResult.CompilerError("CS1022").WithSpan(Source, 50, 1, 50, 2),
                }
            },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 6, 13, 7, 92),
                DiagnosticResult.CompilerError("CS0103").WithSpan(Source, 7, 79, 7, 86).WithArguments("IsGreen")
            },
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }
}
