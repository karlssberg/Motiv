using Microsoft.CodeAnalysis.Testing;
using VerifyCS =
    Motiv.CodeFix.Tests.CSharpCodeFixVerifier<Motiv.Analyzer.MotivAnalyzer, Motiv.CodeFix.MotivCodeFixProvider>;

namespace Motiv.CodeFix.Tests;

public class MotivConvertToSpecWithDebugOutputTests
{
    private const string Source = "Source.cs";

    [Fact]
    public async Task Should_convert_single_variable_with_debug_tap()
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
            using System.Diagnostics;
            using Motiv;

            namespace MyNamespace;

            public class MyClass
            {
                private readonly SpecBase<int, string> _isValidProposition = new IsValidProposition()
                    .Tap((model, result) =>
                        Debug.WriteLine($"[Motiv] IsValidProposition | Model: {model} | Satisfied: {result.Satisfied} | Reason: {result.Reason}"));
                public bool IsValid(int value)
                {
                    // {{booleanExpression}}
                    var isValidResult = _isValidProposition.Evaluate(value);
                    return isValidResult.Satisfied;
                }
            }

            public class IsValidProposition() : Spec<int>(() =>
                Spec.Build((int value) => {{booleanExpression}})
                    .Create("{{booleanExpression}}"));
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            CodeActionEquivalenceKey = "ConvertToSpecWithDebugOutput",
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 7, 16, 7, 16 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_multi_variable_with_debug_tap()
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
                private readonly SpecBase<IsValidProposition.Model, string> _isValidProposition = new IsValidProposition()
                    .Tap((model, result) =>
                        Debug.WriteLine($"[Motiv] IsValidProposition | Model: {model} | Satisfied: {result.Satisfied} | Reason: {result.Reason}"));
                public bool IsValid(int valueA, int valueB, bool valueC)
                {
                    // {{booleanExpression}}
                    var result = _isValidProposition.Evaluate(new IsValidProposition.Model(valueA, valueB, valueC));
                    return result.Satisfied;
                }
            }

            public class IsValidProposition() : Spec<IsValidProposition.Model>(() =>
            {
                var isValueAGreaterThanValueB = Spec
                    .Build((Model m) => m.ValueA > m.ValueB)
                    .Create("{{clause1}}");

                var isValueC = Spec
                    .Build((Model m) => m.ValueC)
                    .Create("{{clause2}}");

                return isValueAGreaterThanValueB.AndAlso(isValueC);
            })
            {
                public record Model(int ValueA, int ValueB, bool ValueC);
            }
            """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            CodeActionEquivalenceKey = "ConvertToSpecWithDebugOutput",
            ExpectedDiagnostics =
            {
                new DiagnosticResult("MOTIV0001", Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithSpan(Source, 5, 65, 5, 65 + booleanExpression.Length)
            }
        }.RunAsync();
    }

    [Fact]
    public async Task Should_convert_instance_methods_with_debug_tap()
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
                using System.Diagnostics;
                using Motiv;

                namespace MyNamespace;

                public class Playground
                {
                    private readonly SpecBase<string, string> _isFeatureEnabledProposition;
                    private readonly SpecBase<string, string> _isGreenProposition = new IsGreenProposition()
                        .Tap((model, result) =>
                            Debug.WriteLine($"[Motiv] IsGreenProposition | Model: {model} | Satisfied: {result.Satisfied} | Reason: {result.Reason}"));
                    public Playground()
                    {
                        _isFeatureEnabledProposition = new IsFeatureEnabledProposition(this)
                            .Tap((model, result) =>
                                Debug.WriteLine($"[Motiv] IsFeatureEnabledProposition | Model: {model} | Satisfied: {result.Satisfied} | Reason: {result.Reason}"));
                    }

                    public bool IsFeatureEnabled(string text)
                    {
                        // {{booleanExpression}}
                        var isFeatureEnabledResult = _isFeatureEnabledProposition.Evaluate(text);
                        return isFeatureEnabledResult.Satisfied;
                    }

                    public bool IsGreen(string text)
                    {
                        // text == "green"
                        var isGreenResult = _isGreenProposition.Evaluate(text);
                        return isGreenResult.Satisfied;
                    }
                }

                public class IsFeatureEnabledProposition(MyNamespace.Playground instance) : Spec<string>(() =>
                {
                    var isNullOrEmpty = Spec
                        .Build((string text) => {{clause1}})
                        .Create("{{clause1}}");

                    var isGreen = Spec
                        .Build((string text) => instance.IsGreen(text))
                        .Create("{{clause2}}");

                    return isNullOrEmpty.AndAlso(isGreen);
                });

                public class IsGreenProposition() : Spec<string>(() =>
                    Spec.Build((string text) => text == "green")
                        .Create("text == \"green\""));
                """;

        await new VerifyCS.Test
        {
            TestState = { Sources = { (Source, source) } },
            FixedState = { Sources = { (Source, expectedTransformedCode) } },
            CodeActionEquivalenceKey = "ConvertToSpecWithDebugOutput",
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
}
