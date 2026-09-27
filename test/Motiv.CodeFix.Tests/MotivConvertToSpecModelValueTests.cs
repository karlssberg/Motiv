using Shouldly;

namespace Motiv.CodeFix.Tests;

/// <summary>
///     Which names in the expression become model values. Only a value read from the expression's own scope can be
///     passed to the model; a name bound to a member of another object, or declared inside the expression, cannot.
/// </summary>
public class MotivConvertToSpecModelValueTests
{
    private static readonly Dictionary<string, string> Sources = new()
    {
        ["RangeVariableDeclaredInside"] =
            """
            using System.Collections.Generic;
            using System.Linq;

            namespace MyNamespace;

            public class Checks
            {
                public bool HasLarge(List<int> items) => [|items.Count > 0 && (from x in items where x > 5 select x).Any()|];
            }
            """,
        ["ConditionalMemberBinding"] =
            """
            namespace MyNamespace;

            public class User
            {
                public bool IsActive { get; set; }
            }

            public class Checks
            {
                public bool IsActiveUser(User? user, int count) => [|user?.IsActive == true && count > 0|];
            }
            """,
        ["PropertyPatternName"] =
            """
            namespace MyNamespace;

            public class Checks
            {
                public bool IsNonEmpty(string text, int count) => [|text is { Length: > 0 } && count > 0|];
            }
            """,
        ["ObjectInitializerTarget"] =
            """
            namespace MyNamespace;

            public class Options
            {
                public bool Strict { get; set; }

                public bool IsValid() => Strict;
            }

            public class Checks
            {
                public bool IsStrictlyValid(int count) => [|new Options { Strict = true }.IsValid() && count > 0|];
            }
            """,
        ["NamesDifferingOnlyInCase"] =
            """
            namespace MyNamespace;

            public class Checks
            {
                public int Limit { get; set; }

                public bool IsWithin(int limit) => [|limit > 0 && limit <= Limit|];
            }
            """
    };

    [Theory]
    [InlineData("RangeVariableDeclaredInside")]
    [InlineData("ConditionalMemberBinding")]
    [InlineData("PropertyPatternName")]
    [InlineData("ObjectInitializerTarget")]
    [InlineData("NamesDifferingOnlyInCase")]
    public async Task Should_produce_compiling_code_when_a_name_is_not_a_value_of_the_expression_scope(string source)
    {
        var outcome = await CodeFixHarness.ApplyFix(Sources[source]);

        outcome.CompilerErrors.ShouldBeEmpty(outcome.FixedSource);
    }
}
