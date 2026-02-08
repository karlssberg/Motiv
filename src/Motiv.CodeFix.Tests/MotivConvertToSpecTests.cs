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
                private readonly Proposition _proposition = new Proposition();
                public bool IsValid(int valueA, int valueB, bool valueC)
                {
                    // {{booleanExpression}}
                    var result = _proposition.IsSatisfiedBy(new Proposition.Model(valueA, valueB, valueC));
                    return result.Satisfied;
                }
            }

            public class Proposition() : Spec<Proposition.Model>(
                Clause1.AndAlso(IsValueC))
            {
                public record Model(int ValueA, int ValueB, bool ValueC);

                private static readonly SpecBase<Model> Clause1 =
                    Spec.Build((Model m) => m.ValueA > m.ValueB)
                        .WhenTrue("{{clause1}} == true")
                        .WhenFalse("{{clause1}} == false")
                        .Create();

                private static readonly SpecBase<Model> IsValueC =
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
                    var result = _isSatisfiedProposition.IsSatisfiedBy(new IsSatisfiedProposition.IsSatisfiedModel(valueA, valueB, valueC));
                    var isSatisfied = result.Satisfied;
                }
            }

            public class IsSatisfiedProposition() : Spec<IsSatisfiedProposition.IsSatisfiedModel>(
                Clause1.AndAlso(IsValueC))
            {
                public record IsSatisfiedModel(int ValueA, int ValueB, bool ValueC);

                private static readonly SpecBase<IsSatisfiedModel> Clause1 =
                    Spec.Build((IsSatisfiedModel m) => m.ValueA > m.ValueB)
                        .WhenTrue("{{clause1}} == true")
                        .WhenFalse("{{clause1}} == false")
                        .Create();

                private static readonly SpecBase<IsSatisfiedModel> IsValueC =
                    Spec.Build((IsSatisfiedModel m) => m.ValueC)
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

    [Fact]
    public async Task Should_convert_many_nested_clauses_into_a_spec()
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

        const string subExpression = $"({clause2} || !({clause3}^{clause4}))";

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private readonly IsSatisfiedProposition _isSatisfiedProposition = new IsSatisfiedProposition();
                public void IsValid(int valueA, int valueB, int valueC)
                {
                    // {{booleanExpression}}
                    var result = _isSatisfiedProposition.IsSatisfiedBy(new IsSatisfiedProposition.IsSatisfiedModel(valueA, valueB, valueC));
                    var isSatisfied = result.Satisfied;
                }
            }

            public class IsSatisfiedProposition() : Spec<IsSatisfiedProposition.IsSatisfiedModel>(
                IsValueANonNegative.AndAlso((IsValueBNonNegative.OrElse(!(IsValueCAtLeast1 ^ IsValueCAtMost10)))))
            {
                public record IsSatisfiedModel(int ValueA, int ValueB, int ValueC);

                private static readonly SpecBase<IsSatisfiedModel> IsValueANonNegative =
                    Spec.Build((IsSatisfiedModel m) => m.ValueA >= 0)
                        .WhenTrue("{{clause1}} == true")
                        .WhenFalse("{{clause1}} == false")
                        .Create();

                private static readonly SpecBase<IsSatisfiedModel> IsValueBNonNegative =
                    Spec.Build((IsSatisfiedModel m) => m.ValueB >= 0)
                        .WhenTrue("{{clause2}} == true")
                        .WhenFalse("{{clause2}} == false")
                        .Create();

                private static readonly SpecBase<IsSatisfiedModel> IsValueCAtLeast1 =
                    Spec.Build((IsSatisfiedModel m) => m.ValueC >= 1)
                        .WhenTrue("{{clause3}} == true")
                        .WhenFalse("{{clause3}} == false")
                        .Create();

                private static readonly SpecBase<IsSatisfiedModel> IsValueCAtMost10 =
                    Spec.Build((IsSatisfiedModel m) => m.ValueC <= 10)
                        .WhenTrue("{{clause4}} == true")
                        .WhenFalse("{{clause4}} == false")
                        .Create();
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
                private readonly Proposition _proposition = new Proposition();
                public bool IsValid(int x, int y)
                {
                    // {{booleanExpression}}
                    var result = _proposition.IsSatisfiedBy(new Proposition.Model(x, y));
                    return result.Satisfied;
                }
            }

            public class Proposition() : Spec<Proposition.Model>(
                IsXGreaterThan5.AndAlso(IsYLessThan10))
            {
                public record Model(int X, int Y);

                private static readonly SpecBase<Model> IsXGreaterThan5 =
                    Spec.Build((Model m) => m.X > 5)
                        .WhenTrue("{{clause1}} == true")
                        .WhenFalse("{{clause1}} == false")
                        .Create();

                private static readonly SpecBase<Model> IsYLessThan10 =
                    Spec.Build((Model m) => m.Y < 10)
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
                    var result = _isSatisfiedProposition.IsSatisfiedBy(new IsSatisfiedProposition.IsSatisfiedModel(age, name));
                    var isSatisfied = result.Satisfied;
                }
            }

            public class IsSatisfiedProposition() : Spec<IsSatisfiedProposition.IsSatisfiedModel>(
                IsAgePositive.AndAlso(IsName).AndAlso(IsAgePositive))
            {
                public record IsSatisfiedModel(int Age, bool Name);

                private static readonly SpecBase<IsSatisfiedModel> IsAgePositive =
                    Spec.Build((IsSatisfiedModel m) => m.Age > 0)
                        .WhenTrue("{{clause1}} == true")
                        .WhenFalse("{{clause1}} == false")
                        .Create();

                private static readonly SpecBase<IsSatisfiedModel> IsName =
                    Spec.Build((IsSatisfiedModel m) => m.Name)
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
        const string booleanExpression = $"({clause1} && {clause2}) || ({clause3} && {clause2})";

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

        const string expectedTransformedCode =
          $$"""
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private readonly IsSatisfiedProposition _isSatisfiedProposition = new IsSatisfiedProposition();
                public void IsValid(int valueA, int valueB, int valueC)
                {
                    // {{booleanExpression}}
                    var result = _isSatisfiedProposition.IsSatisfiedBy(new IsSatisfiedProposition.IsSatisfiedModel(valueA, valueC, valueB));
                    var isSatisfied = result.Satisfied;
                }
            }

            public class IsSatisfiedProposition() : Spec<IsSatisfiedProposition.IsSatisfiedModel>(
                (IsValueANonNegative.AndAlso(IsValueCAtLeast1)).OrElse((IsValueBNonNegative.AndAlso(IsValueCAtLeast1))))
            {
                public record IsSatisfiedModel(int ValueA, int ValueC, int ValueB);

                private static readonly SpecBase<IsSatisfiedModel> IsValueANonNegative =
                    Spec.Build((IsSatisfiedModel m) => m.ValueA >= 0)
                        .WhenTrue("{{clause1}} == true")
                        .WhenFalse("{{clause1}} == false")
                        .Create();

                private static readonly SpecBase<IsSatisfiedModel> IsValueCAtLeast1 =
                    Spec.Build((IsSatisfiedModel m) => m.ValueC >= 1)
                        .WhenTrue("{{clause2}} == true")
                        .WhenFalse("{{clause2}} == false")
                        .Create();

                private static readonly SpecBase<IsSatisfiedModel> IsValueBNonNegative =
                    Spec.Build((IsSatisfiedModel m) => m.ValueB >= 0)
                        .WhenTrue("{{clause3}} == true")
                        .WhenFalse("{{clause3}} == false")
                        .Create();
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
    public async Task Should_deduplicate_clauses_with_less_than_comparison()
    {
        const string clause1 = "valueA >= 0";
        const string clause2 = "1 < valueC";
        const string clause3 = "valueB >= 0";
        const string booleanExpression = $"({clause1} && {clause2}) || ({clause3} && {clause2})";

        const string source =
            $$"""
              namespace MyNamespace;

              public class MyClass
              {
                  public bool IsFeatureEnabled(int valueA, int valueB, int valueC)
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
                private readonly IsFeatureEnabledProposition _isFeatureEnabledProposition = new IsFeatureEnabledProposition();
                public bool IsFeatureEnabled(int valueA, int valueB, int valueC)
                {
                    // {{booleanExpression}}
                    var result = _isFeatureEnabledProposition.IsSatisfiedBy(new IsFeatureEnabledProposition.IsFeatureEnabledModel(valueA, valueC, valueB));
                    return result.Satisfied;
                }
            }

            public class IsFeatureEnabledProposition() : Spec<IsFeatureEnabledProposition.IsFeatureEnabledModel>(
                (IsValueANonNegative.AndAlso(Is1LessThanValueC)).OrElse((IsValueBNonNegative.AndAlso(Is1LessThanValueC))))
            {
                public record IsFeatureEnabledModel(int ValueA, int ValueC, int ValueB);

                private static readonly SpecBase<IsFeatureEnabledModel> IsValueANonNegative =
                    Spec.Build((IsFeatureEnabledModel m) => m.ValueA >= 0)
                        .WhenTrue("{{clause1}} == true")
                        .WhenFalse("{{clause1}} == false")
                        .Create();

                private static readonly SpecBase<IsFeatureEnabledModel> Is1LessThanValueC =
                    Spec.Build((IsFeatureEnabledModel m) => 1 < m.ValueC)
                        .WhenTrue("{{clause2}} == true")
                        .WhenFalse("{{clause2}} == false")
                        .Create();

                private static readonly SpecBase<IsFeatureEnabledModel> IsValueBNonNegative =
                    Spec.Build((IsFeatureEnabledModel m) => m.ValueB >= 0)
                        .WhenTrue("{{clause3}} == true")
                        .WhenFalse("{{clause3}} == false")
                        .Create();
            }

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
}
