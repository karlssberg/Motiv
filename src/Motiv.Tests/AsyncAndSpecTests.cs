namespace Motiv.Tests;

public class AsyncAndSpecTests
{
    private static AsyncPolicyBase<object, string> Async(string name, bool value) =>
        Spec.BuildAsync((object _) => new ValueTask<bool>(value))
            .WhenTrue(true.ToString()).WhenFalse(false.ToString())
            .Create(name);

    private static PolicyBase<object, string> Sync(string name, bool value) =>
        Spec.Build((object _) => value)
            .WhenTrue(true.ToString()).WhenFalse(false.ToString())
            .Create(name);

    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(false, true, false)]
    [InlineAutoData(false, false, false)]
    public async Task Should_perform_logical_and_with_sync_parity(
        bool leftValue, bool rightValue, bool expected, object model)
    {
        // Arrange
        var syncSpec = Sync("left", leftValue) & Sync("right", rightValue);
        var asyncSpec = Async("left", leftValue) & Async("right", rightValue);

        // Act
        var syncResult = syncSpec.Evaluate(model);
        var asyncResult = await asyncSpec.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(expected);
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Fact]
    public void Should_produce_the_same_statement_as_the_sync_and()
    {
        // Arrange
        var syncSpec = Sync("left", true) & Sync("right", true);
        var asyncSpec = Async("left", true) & Async("right", true);

        // Act & Assert
        asyncSpec.Description.Statement.ShouldBe(syncSpec.Description.Statement);
        asyncSpec.ToString().ShouldBe(syncSpec.ToString());
    }

    [Fact]
    public void Should_collapse_nested_and_statements_like_sync()
    {
        // Arrange
        var syncSpec = (Sync("a", true) & Sync("b", true)) & Sync("c", true);
        var asyncSpec = (Async("a", true) & Async("b", true)) & Async("c", true);

        // Act & Assert
        asyncSpec.Description.Statement.ShouldBe(syncSpec.Description.Statement);
        asyncSpec.Expression.ShouldBe(syncSpec.Expression);
    }

    [Fact]
    public async Task Should_render_lifted_sync_operands_identically_to_all_sync_composition()
    {
        // Arrange — a sync OR lifted into an async AND must parenthesize/collapse as in sync
        var syncOr = Sync("x", true) | Sync("y", false);
        var syncSpec = syncOr & Sync("z", true);
        var asyncSpec = syncOr.ToAsyncSpec() & Async("z", true);

        // Act
        var syncResult = syncSpec.Evaluate(new object());
        var asyncResult = await asyncSpec.EvaluateAsync(new object());

        // Assert
        asyncSpec.Description.Statement.ShouldBe(syncSpec.Description.Statement);
        asyncSpec.Expression.ShouldBe(syncSpec.Expression);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Fact]
    public async Task Should_evaluate_sequentially_left_then_right()
    {
        // Arrange
        var order = new List<string>();
        var left = Spec.BuildAsync(async (object _) =>
        {
            await Task.Yield();
            order.Add("left");
            return true;
        }).Create("left");
        var right = Spec.BuildAsync(async (object _) =>
        {
            await Task.Yield();
            order.Add("right");
            return true;
        }).Create("right");

        // Act
        await (left & right).EvaluateAsync(new object());

        // Assert
        order.ShouldBe(["left", "right"]);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(false, false)]
    public async Task Should_match_via_the_boolean_only_path(bool value, bool expected, object model)
    {
        // Arrange
        var spec = Async("left", value) & Async("right", true);

        // Act & Assert
        (await spec.MatchesAsync(model)).ShouldBe(expected);
    }
}
