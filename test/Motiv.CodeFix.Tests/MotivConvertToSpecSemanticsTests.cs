using System.Reflection;
using Shouldly;

namespace Motiv.CodeFix.Tests;

/// <summary>
///     Compiles each expression before and after the fix and runs both over every input, asserting the converted
///     spec decides exactly as the expression did. "It compiles" cannot see a fix that changes the answer.
/// </summary>
public class MotivConvertToSpecSemanticsTests
{
    private static readonly bool[] Booleans = [false, true];
    private static readonly int[] Integers = [-1, 1, 10];

    [Theory]
    [InlineData("a && b")]
    [InlineData("!a && !b")]
    [InlineData("!a && b")]
    [InlineData("a && !b")]
    [InlineData("!a || !b")]
    [InlineData("!(a && b) || c")]
    [InlineData("a || b && c")]
    [InlineData("(a || b) && c")]
    [InlineData("a && (b || !c)")]
    [InlineData("!a || !b && c")]
    [InlineData("!(a || b) && !c")]
    [InlineData("(a ^ b) && c")]
    [InlineData("a ^ b && c")]
    [InlineData("Equals(a, b) && !Equals(b, c)")]
    [InlineData("x > 0 && !(y > 0)")]
    [InlineData("!(x > 0) && y < 5")]
    [InlineData("x > 0 && y > 0 || x < 0 && y < 0")]
    [InlineData("!(x > 0 || y > 0) || a")]
    public async Task Should_decide_as_the_original_expression_did_for_every_input(string expression)
    {
        var source =
          $$"""
            namespace MyNamespace;

            public static class Checks
            {
                public static bool Check(bool a, bool b, bool c, int x, int y) => [|{{expression}}|];
            }
            """;

        var outcome = await CodeFixHarness.ApplyFix(source);
        outcome.CompilerErrors.ShouldBeEmpty(outcome.FixedSource);

        var original = GetCheck(await CodeFixHarness.Load(source));
        var converted = GetCheck(await CodeFixHarness.Load(outcome.FixedSource));

        foreach (var arguments in AllInputs())
        {
            converted.Invoke(null, arguments).ShouldBe(
                original.Invoke(null, arguments),
                $"({string.Join(", ", arguments)})\n{outcome.FixedSource}");
        }
    }

    private static MethodInfo GetCheck(Assembly assembly) =>
        assembly.GetType("MyNamespace.Checks")!.GetMethod("Check")!;

    private static IEnumerable<object[]> AllInputs() =>
        from a in Booleans
        from b in Booleans
        from c in Booleans
        from x in Integers
        from y in Integers
        select new object[] { a, b, c, x, y };
}
