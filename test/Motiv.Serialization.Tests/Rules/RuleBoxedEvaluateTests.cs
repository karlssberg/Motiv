namespace Motiv.Serialization.Tests.Rules;

/// <summary>
/// <see cref="RuleBase.EvaluateBoxedAsync"/> is how a caller holding only a <see cref="RuleBase"/> —
/// a rule found by name — evaluates it without knowing its generic arguments. It has to project
/// exactly what the typed entry point projects, on both rule flavours.
/// </summary>
public class RuleBoxedEvaluateTests
{
    private static SpecBase<int, string> Positive { get; } =
        Spec.Build((int n) => n > 0).WhenTrue("positive").WhenFalse("not positive").Create();

    private static AsyncSpecBase<int, string> PositiveAsync { get; } =
        Spec.BuildAsync(async (int n) => { await Task.Yield(); return n > 0; })
            .WhenTrue("positive").WhenFalse("not positive").Create();

    private sealed class NumberRule() : Rule<int, string>("number", Positive);

    private sealed class NumberAsyncRule() : AsyncRule<int, string>("number-async", PositiveAsync);

    private static RuleSet Bound(RuleBase rule)
    {
        var rules = new RuleSet(new SpecRegistry()).Add(rule);
        rules.Load();
        return rules;
    }

    [Fact]
    public async Task Should_evaluate_a_sync_rule_found_by_name_and_project_the_typed_result()
    {
        // Arrange
        var rules = Bound(new NumberRule());
        var rule = rules.Find("number")!;

        // Act
        var boxed = await rule.EvaluateBoxedAsync(-3, new ResultSerializer(), CancellationToken.None);

        // Assert
        var result = boxed.ShouldBeOfType<RuleEvaluationResult<string>>();
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["not positive"]);
    }

    [Fact]
    public async Task Should_evaluate_an_async_rule_found_by_name_and_project_the_typed_result()
    {
        // Arrange
        var rules = Bound(new NumberAsyncRule());
        var rule = rules.Find("number-async")!;

        // Act
        var boxed = await rule.EvaluateBoxedAsync(7, new ResultSerializer(), CancellationToken.None);

        // Assert
        var result = boxed.ShouldBeOfType<RuleEvaluationResult<string>>();
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["positive"]);
    }

    [Fact]
    public async Task Should_refuse_a_model_of_the_wrong_type()
    {
        // Arrange
        var rules = Bound(new NumberRule());
        var rule = rules.Find("number")!;

        // Act / Assert
        await Should.ThrowAsync<InvalidCastException>(
            async () => await rule.EvaluateBoxedAsync("seven", new ResultSerializer(), CancellationToken.None));
    }
}
