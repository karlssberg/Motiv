namespace Motiv.Tests;

public class AsyncDecoratorExplanationTests
{
    private static SpecBase<object, string> Sync(bool value) =>
        Spec.Build((object _) => value).Create("inner");

    private static AsyncSpecBase<object, string> Async(bool value) =>
        Spec.BuildAsync((object _) => new ValueTask<bool>(value)).Create("inner");

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_use_because_strings_as_assertions_when_unnamed(bool value, object model)
    {
        // Arrange
        var sync = Spec.Build(Sync(value)).WhenTrue("is on").WhenFalse("is off").Create();
        var async = Spec.Build(Async(value)).WhenTrue("is on").WhenFalse("is off").Create();

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await async.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);   // ["is on"] / ["is off"]
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_demote_because_strings_to_values_when_named(bool value, object model)
    {
        // Arrange
        var sync = Spec.Build(Sync(value)).WhenTrue("is on").WhenFalse("is off").Create("power");
        var async = Spec.Build(Async(value)).WhenTrue("is on").WhenFalse("is off").Create("power");

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await async.EvaluateAsync(model);

        // Assert — named: "power == true|false" assertions, strings become Values
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Values.ShouldBe(syncResult.Values);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_support_model_function_when_false(bool value, object model)
    {
        // Arrange
        var sync = Spec.Build(Sync(value)).WhenTrue("is on").WhenFalse(_ => "is off").Create();
        var async = Spec.Build(Async(value)).WhenTrue("is on").WhenFalse(_ => "is off").Create();

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await async.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_support_result_consuming_when_false(bool value, object model)
    {
        // Arrange — falseBecause consumes the underlying result
        var sync = Spec.Build(Sync(value))
            .WhenTrue("is on")
            .WhenFalse((_, r) => $"denied: {r.Reason}")
            .Create();
        var async = Spec.Build(Async(value))
            .WhenTrue("is on")
            .WhenFalse((_, r) => $"denied: {r.Reason}")
            .Create();

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await async.EvaluateAsync(model);

        // Assert — on the false path the assertion derives from the underlying result's reason
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        if (!value)
            asyncResult.Assertions.ShouldBe(["denied: inner == false"]);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Fact]
    public async Task Should_create_an_async_policy_from_singular_when_true_false()
    {
        // Arrange & Act
        var whenTrue = Spec.Build(Async(true)).WhenTrue("t").WhenFalse("f").Create();
        var whenFalse = Spec.Build(Async(false)).WhenTrue("t").WhenFalse("f").Create();

        // Assert — singular WhenTrue/WhenFalse yields a policy value on both paths, as in sync
        var trueResult = await whenTrue.EvaluateAsync(new object());
        trueResult.Value.ShouldBe("t");
        var falseResult = await whenFalse.EvaluateAsync(new object());
        falseResult.Value.ShouldBe("f");
    }

    [Fact]
    public void Should_throw_when_unnamed_create_has_whitespace_true_because()
    {
        // Act — mirrors the sync factory's guard: the WhenTrue string doubles as the statement
        var act = () => Spec.Build(Async(true)).WhenTrue(" ").WhenFalse("f").Create();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }
}
