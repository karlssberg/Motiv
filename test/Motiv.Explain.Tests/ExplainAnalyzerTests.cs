using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Motiv.Explain.Tests;

public class ExplainAnalyzerTests
{
    // What the generator always adds to a compilation; spelled out here because analyzer tests run alone.
    private const string Attribute =
        """
        namespace Motiv.Explain
        {
            [System.AttributeUsage(System.AttributeTargets.Method)]
            internal sealed class ExplainAttribute : System.Attribute { }
        }
        """;

    private static Task Verify(string source, params DiagnosticResult[] expected) =>
        Verify([source, Attribute], expected);

    private static async Task Verify(string[] sources, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ExplainAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        foreach (var source in sources)
            test.TestState.Sources.Add(source);
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }

    private static DiagnosticResult Diagnostic(string id) =>
        new DiagnosticResult(id, Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);

    [Fact]
    public async Task An_explainable_method_and_its_direct_calls_raise_nothing()
    {
        await Verify(
            """
            using Motiv.Explain;

            public partial class Pricing
            {
                private readonly int _limit = 10;

                [Explain]
                public bool IsBig(int n) => n > _limit && n % 2 == 0;

                public bool Check(int n) => IsBig(n) || this.IsBig(n + 1);
            }
            """);
    }

    [Fact]
    public async Task Using_an_explain_method_as_a_delegate_warns_that_it_will_not_be_explained()
    {
        await Verify(
            """
            using System;
            using System.Linq;
            using Motiv.Explain;

            public static partial class Rules
            {
                [Explain]
                public static bool IsBig(int n) => n > 10;

                public static int Count(int[] values) => values.Count({|#0:IsBig|});
                public static Func<int, bool> Get() => {|#1:Rules.IsBig|};
            }
            """,
            Diagnostic("MOTIV1001").WithLocation(0).WithArguments("Rules.IsBig(int)"),
            Diagnostic("MOTIV1001").WithLocation(1).WithArguments("Rules.IsBig(int)"));
    }

    [Fact]
    public async Task Calling_an_explain_method_from_another_project_warns_that_it_will_not_be_explained()
    {
        var test = new CSharpAnalyzerTest<ExplainAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.TestState.AdditionalProjects["Library"].Sources.Add(Attribute.Replace("internal sealed", "public sealed"));
        test.TestState.AdditionalProjects["Library"].Sources.Add(
            """
            namespace Library
            {
                public static partial class Rules
                {
                    [Motiv.Explain.Explain]
                    public static bool IsBig(int n) => n > 10;
                }
            }
            """);
        test.TestState.AdditionalProjectReferences.Add("Library");
        test.TestState.Sources.Add(
            """
            public static class Caller
            {
                public static bool Check(int n) => {|#0:Library.Rules.IsBig(n)|};
            }
            """);
        test.ExpectedDiagnostics.Add(Diagnostic("MOTIV1002").WithLocation(0).WithArguments("Library.Rules.IsBig(int)"));

        await test.RunAsync();
    }

    [Fact]
    public async Task An_explain_method_in_a_type_that_is_not_partial_is_reported()
    {
        await Verify(
            """
            using Motiv.Explain;

            public partial class Outer
            {
                public class Inner
                {
                    [Explain]
                    public static bool {|#0:IsBig|}(int n) => n > 10;
                }
            }
            """,
            Diagnostic("MOTIV1003").WithLocation(0).WithArguments("Outer.Inner.IsBig(int)", "Outer.Inner"));
    }

    [Theory]
    [InlineData("public static int {|#0:Size|}(int n) => n;", "Rules.Size(int)", "it doesn't return bool")]
    [InlineData("public static bool {|#0:IsBig|}(int n) { var m = n; return m > 10; }", "Rules.IsBig(int)", "its body isn't a single expression")]
    [InlineData("public static bool {|#0:IsDefault|}<T>(T value) => value is null;", "Rules.IsDefault<T>(T)", "generic methods and types aren't supported")]
    [InlineData("public static bool {|#0:IsBig|}(ref int n) => n > 10;", "Rules.IsBig(ref int)", "it has a ref, out or in parameter")]
    [InlineData("public virtual bool {|#0:IsBig|}(int n) => n > 10;", "Rules.IsBig(int)", "it can be overridden, so a call might not run this body")]
    [InlineData("public bool {|#0:IsBig|}(int n) => base.Equals(n);", "Rules.IsBig(int)", "it uses base")]
    [InlineData("public static bool {|#0:IsEmpty|}(System.ReadOnlySpan<int> values) => values.Length == 0;", "Rules.IsEmpty(System.ReadOnlySpan<int>)", "a parameter is a ref struct or pointer")]
    [InlineData("private static bool {|#0:IsSecret|}(Secret s) => s != null; private class Secret { }", "Rules.IsSecret(Rules.Secret)", "a parameter's type isn't visible to the rest of the assembly")]
    public async Task A_method_that_cannot_be_explained_says_why(string member, string method, string reason)
    {
        await Verify(
            $$"""
            using Motiv.Explain;

            public partial class Rules
            {
                [Explain]
                {{member}}
            }
            """,
            Diagnostic("MOTIV1004").WithLocation(0).WithArguments(method, reason));
    }

    [Fact]
    public async Task An_extension_method_cannot_be_explained_yet()
    {
        await Verify(
            """
            using Motiv.Explain;

            public static partial class IntExtensions
            {
                [Explain]
                public static bool {|#0:IsBig|}(this int n) => n > 10;
            }
            """,
            Diagnostic("MOTIV1004").WithLocation(0).WithArguments("IntExtensions.IsBig(int)", "extension methods aren't supported"));
    }

    [Fact]
    public async Task A_struct_method_cannot_be_explained()
    {
        await Verify(
            """
            using Motiv.Explain;

            public partial struct Point
            {
                public int X;

                [Explain]
                public bool {|#0:IsRight|}() => X > 0;
            }
            """,
            Diagnostic("MOTIV1004").WithLocation(0).WithArguments("Point.IsRight()", "it is declared in a struct or interface"));
    }

    [Fact]
    public async Task A_local_function_cannot_be_explained()
    {
        await Verify(
            """
            using Motiv.Explain;

            public partial class Rules
            {
                public static bool Check(int n)
                {
                    return IsBig(n);

                    [Explain]
                    static bool {|#0:IsBig|}(int value) => value > 10;
                }
            }
            """,
            Diagnostic("MOTIV1004").WithLocation(0).WithArguments("IsBig(int)", "it is a local function"));
    }

    [Fact]
    public async Task A_sealed_override_can_be_explained()
    {
        await Verify(
            """
            using Motiv.Explain;

            public abstract class Rule { public abstract bool Applies(int n); }

            public sealed partial class BigRule : Rule
            {
                [Explain]
                public override bool Applies(int n) => n > 10;
            }
            """);
    }
}
