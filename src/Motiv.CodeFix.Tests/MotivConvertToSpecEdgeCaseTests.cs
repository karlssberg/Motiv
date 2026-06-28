using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Motiv.CodeFix.Tests.CSharpCodeFixVerifier<Motiv.Analyzer.MotivAnalyzer, Motiv.CodeFix.MotivCodeFixProvider>;

namespace Motiv.CodeFix.Tests;

public class MotivConvertToSpecEdgeCaseTests
{
    private const string Source = "Source.cs";

    [Fact]
    public async Task Should_convert_is_not_null_pattern_with_member_access()
    {
        const string booleanExpression = "text is not null && text.Length > 0";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(string text)
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
                private readonly IsValidProposition _isValidProposition = new();
                public bool IsValid(string text)
                {
                    // {{booleanExpression}}
                    var isValidResult = _isValidProposition.Evaluate(text);
                    return isValidResult.Satisfied;
                }
            }

            public class IsValidProposition() : Spec<string>(() =>
                Spec.Build((string text) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_deep_property_chain_expression()
    {
        const string booleanExpression = "order.Address.City == \"NYC\"";

        const string source =
          $$"""
            namespace MyNamespace;

            public class Address
            {
                public string City { get; set; }
            }

            public class Order
            {
                public Address Address { get; set; }
            }

            public class MyClass
            {
                public bool IsValid(Order order)
                {
                    return {{booleanExpression}};
                }
            }
            """;

        const string escapedExpression = "order.Address.City == \\\"NYC\\\"";
        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class Address
            {
                public string City { get; set; }
            }

            public class Order
            {
                public Address Address { get; set; }
            }

            public class MyClass
            {
                private readonly IsValidProposition _isValidProposition = new();
                public bool IsValid(Order order)
                {
                    // order.Address.City == "NYC"
                    var isValidResult = _isValidProposition.Evaluate(order);
                    return isValidResult.Satisfied;
                }
            }

            public class IsValidProposition() : Spec<MyNamespace.Order>(() =>
                Spec.Build((MyNamespace.Order order) => {{booleanExpression}})
                    .Create("{{escapedExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 17, 16, 17, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_cast_expression_in_comparison()
    {
        const string booleanExpression = "(double)value > 0.5";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsValid(object value)
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
                private readonly IsValidProposition _isValidProposition = new();
                public bool IsValid(object value)
                {
                    // {{booleanExpression}}
                    var isValidResult = _isValidProposition.Evaluate(value);
                    return isValidResult.Satisfied;
                }
            }

            public class IsValidProposition() : Spec<object>(() =>
                Spec.Build((object value) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Theory]
    [InlineData("name == nameof(MyClass)")]
    [InlineData("typeof(MyClass).Name == name")]
    [InlineData("name != default(string)")]
    public async Task Should_convert_language_keyword_expression_in_comparison(string booleanExpression)
    {
        var source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsMatch(string name)
                {
                    return {{booleanExpression}};
                }
            }
            """;

        var expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private readonly IsMatchProposition _isMatchProposition = new();
                public bool IsMatch(string name)
                {
                    // {{booleanExpression}}
                    var isMatchResult = _isMatchProposition.Evaluate(name);
                    return isMatchResult.Satisfied;
                }
            }

            public class IsMatchProposition() : Spec<string>(() =>
                Spec.Build((string name) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_range_check_with_same_variable()
    {
        const string booleanExpression = "x >= 0 && x <= 100";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsInRange(int x)
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
                private readonly IsInRangeProposition _isInRangeProposition = new();
                public bool IsInRange(int x)
                {
                    // {{booleanExpression}}
                    var isInRangeResult = _isInRangeProposition.Evaluate(x);
                    return isInRangeResult.Satisfied;
                }
            }

            public class IsInRangeProposition() : Spec<int>(() =>
                Spec.Build((int x) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_pattern_match_with_declared_variable()
    {
        const string booleanExpression = "obj is string s && s.Length > 0";

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
                private readonly IsValidProposition _isValidProposition = new();
                public bool IsValid(object obj)
                {
                    // {{booleanExpression}}
                    var isValidResult = _isValidProposition.Evaluate(obj);
                    return isValidResult.Satisfied;
                }
            }

            public class IsValidProposition() : Spec<object>(() =>
                Spec.Build((object obj) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_triple_and_chain_with_three_variables()
    {
        const string clause1 = "a > 0";
        const string clause2 = "b > 0";
        const string clause3 = "c > 0";
        const string booleanExpression = $"{clause1} && {clause2} && {clause3}";

        const string source =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public void Check(int a, int b, int c)
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
                private readonly IsSatisfiedProposition _isSatisfiedProposition = new();
                public void Check(int a, int b, int c)
                {
                    // {{booleanExpression}}
                    var isSatisfiedResult = _isSatisfiedProposition.Evaluate(new IsSatisfiedProposition.Model(a, b, c));
                    var isSatisfied = isSatisfiedResult.Satisfied;
                }
            }

            public class IsSatisfiedProposition() : Spec<IsSatisfiedProposition.Model>(() =>
            {
                var isAPositive = Spec
                    .Build((Model m) => m.A > 0)
                    .Create("{{clause1}}");

                var isBPositive = Spec
                    .Build((Model m) => m.B > 0)
                    .Create("{{clause2}}");

                var isCPositive = Spec
                    .Build((Model m) => m.C > 0)
                    .Create("{{clause3}}");

                return isAPositive.AndAlso(isBPositive)
                    .AndAlso(isCPositive);
            })
            {
                public record Model(int A, int B, int C);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 27, 7, 27 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_expression_with_extra_nested_parentheses()
    {
        const string clause1 = "a > 0";
        const string clause2 = "b < 10";
        const string booleanExpression = $"(({clause1})) && (({clause2}))";

        const string source =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public void Check(int a, int b)
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
                private readonly IsSatisfiedProposition _isSatisfiedProposition = new();
                public void Check(int a, int b)
                {
                    // {{booleanExpression}}
                    var isSatisfiedResult = _isSatisfiedProposition.Evaluate(new IsSatisfiedProposition.Model(a, b));
                    var isSatisfied = isSatisfiedResult.Satisfied;
                }
            }

            public class IsSatisfiedProposition() : Spec<IsSatisfiedProposition.Model>(() =>
            {
                var isAPositive = Spec
                    .Build((Model m) => m.A > 0)
                    .Create("{{clause1}}");

                var isBLessThan10 = Spec
                    .Build((Model m) => m.B < 10)
                    .Create("{{clause2}}");

                return ((isAPositive)).AndAlso(((isBLessThan10)));
            })
            {
                public record Model(int A, int B);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 27, 7, 27 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_multiple_is_type_patterns()
    {
        const string booleanExpression = "obj is int || obj is string";

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
                private readonly IsValidProposition _isValidProposition = new();
                public bool IsValid(object obj)
                {
                    // obj is int ||
                    //     obj is string
                    var isValidResult = _isValidProposition.Evaluate(obj);
                    return isValidResult.Satisfied;
                }
            }

            public class IsValidProposition() : Spec<object>(() =>
                Spec.Build((object obj) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_preserve_negation_when_expression_is_wrapped_in_not_operator()
    {
        const string booleanExpression = "!(a > 0 && b > 0)";

        const string source =
          $$"""
            namespace MyNamespace;

            public class MyClass
            {
                public bool IsInvalid(int a, int b)
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
                private readonly IsInvalidProposition _isInvalidProposition = new();
                public bool IsInvalid(int a, int b)
                {
                    // {{booleanExpression}}
                    var isInvalidResult = _isInvalidProposition.Evaluate(new IsInvalidProposition.Model(a, b));
                    return isInvalidResult.Satisfied;
                }
            }

            public class IsInvalidProposition() : Spec<IsInvalidProposition.Model>(() =>
            {
                var isAPositive = Spec
                    .Build((Model m) => m.A > 0)
                    .Create("a > 0");

                var isBPositive = Spec
                    .Build((Model m) => m.B > 0)
                    .Create("b > 0");

                return !(isAPositive.AndAlso(isBPositive));
            })
            {
                public record Model(int A, int B);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }
}
