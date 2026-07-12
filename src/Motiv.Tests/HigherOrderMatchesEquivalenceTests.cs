namespace Motiv.Tests;

public class HigherOrderMatchesEquivalenceTests
{
    private static readonly int[][] Datasets =
    [
        [], [1], [-1], [1, 2, 3], [-1, -2, -3], [1, -2, 3], [1, 2, -3, 4, -5]
    ];

    // Built inside the test (not passed via MemberData) so theory args stay serializable.
    private static SpecBase<IEnumerable<int>, string> Build(string op) =>
        op switch
        {
            "All" => Spec.Build((int n) => n > 0).AsAllSatisfied().Create("all"),
            "Any" => Spec.Build((int n) => n > 0).AsAnySatisfied().Create("any"),
            "None" => Spec.Build((int n) => n > 0).AsNoneSatisfied().Create("none"),
            "AtLeast0" => Spec.Build((int n) => n > 0).AsAtLeastNSatisfied(0).Create("al0"),
            "AtLeast2" => Spec.Build((int n) => n > 0).AsAtLeastNSatisfied(2).Create("al2"),
            "AtMost0" => Spec.Build((int n) => n > 0).AsAtMostNSatisfied(0).Create("am0"),
            "AtMost2" => Spec.Build((int n) => n > 0).AsAtMostNSatisfied(2).Create("am2"),
            "Exactly0" => Spec.Build((int n) => n > 0).AsNSatisfied(0).Create("n0"),
            "Exactly2" => Spec.Build((int n) => n > 0).AsNSatisfied(2).Create("n2"),
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

    [Theory]
    [InlineData("All")]
    [InlineData("Any")]
    [InlineData("None")]
    [InlineData("AtLeast0")]
    [InlineData("AtLeast2")]
    [InlineData("AtMost0")]
    [InlineData("AtMost2")]
    [InlineData("Exactly0")]
    [InlineData("Exactly2")]
    public void Matches_equals_Evaluate_Satisfied_across_datasets(string op)
    {
        var spec = Build(op);

        foreach (var data in Datasets)
            spec.Matches(data).ShouldBe(spec.Evaluate(data).Satisfied, $"{op} on [{string.Join(",", data)}]");
    }
}
