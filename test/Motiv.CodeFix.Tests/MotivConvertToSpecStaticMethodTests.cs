using Shouldly;

namespace Motiv.CodeFix.Tests;

/// <summary>
///     Expressions that call a static method of the type they sit in. The spec is declared beside that type, so the
///     call must be qualified with the type's name as the spec's position sees it.
/// </summary>
public class MotivConvertToSpecStaticMethodTests
{
    private static readonly Dictionary<string, string> Sources = new()
    {
        ["SingleValueFileScopedNamespace"] =
            """
            namespace MyNamespace;

            public class Checks
            {
                public bool IsWithin(int count) => [|count > 0 && IsSmall(5)|];

                public static bool IsSmall(int n) => n < 10;
            }
            """,
        ["SingleValueBlockNamespace"] =
            """
            namespace MyNamespace
            {
                public class Checks
                {
                    public bool IsWithin(int count) => [|count > 0 && IsSmall(5)|];

                    public static bool IsSmall(int n) => n < 10;
                }
            }
            """,
        ["SingleValueArgumentFileScopedNamespace"] =
            """
            namespace MyNamespace;

            public class Checks
            {
                public bool IsWithin(int count) => [|count > 0 && IsSmall(count)|];

                public static bool IsSmall(int n) => n < 10;
            }
            """,
        ["MultipleValuesFileScopedNamespace"] =
            """
            namespace MyNamespace;

            public class Checks
            {
                public bool IsWithin(int count, int limit) => [|count > 0 && IsSmall(limit)|];

                public static bool IsSmall(int n) => n < 10;
            }
            """,
        ["NestedTypeBlockNamespace"] =
            """
            namespace MyNamespace
            {
                public class Outer
                {
                    public class Checks
                    {
                        public bool IsWithin(int count, int limit) => [|count > 0 && IsSmall(limit)|];

                        public static bool IsSmall(int n) => n < 10;
                    }
                }
            }
            """,
        ["InternalMethod"] =
            """
            namespace MyNamespace;

            public class Checks
            {
                public bool IsWithin(int count) => [|count > 0 && IsSmall(count)|];

                internal static bool IsSmall(int n) => n < 10;
            }
            """,
    };

    [Theory]
    [InlineData("SingleValueFileScopedNamespace")]
    [InlineData("SingleValueBlockNamespace")]
    [InlineData("SingleValueArgumentFileScopedNamespace")]
    [InlineData("MultipleValuesFileScopedNamespace")]
    [InlineData("NestedTypeBlockNamespace")]
    [InlineData("InternalMethod")]
    public async Task Should_produce_compiling_code_when_the_expression_calls_a_static_method_of_its_type(
        string context)
    {
        var outcome = await CodeFixHarness.ApplyFix(Sources[context]);

        outcome.CompilerErrors.ShouldBeEmpty(outcome.FixedSource);
    }
}
