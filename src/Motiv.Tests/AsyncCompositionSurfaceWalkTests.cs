namespace Motiv.Tests;

/// <summary>
/// Exercises every trivially-delegating member of the untyped <see cref="AsyncSpecBase{TModel}" /> composition
/// surface, plus the two typed mixed-operand operators on <see cref="AsyncSpecBase{TModel,TMetadata}" />, so
/// that the codecov patch gate sees coverage for lines no other test invokes directly. Rendering correctness
/// (Reason/Assertions/Justification) is owned by the equivalence matrix — this walk intentionally only asserts
/// <c>.Satisfied</c>.
/// </summary>
public class AsyncCompositionSurfaceWalkTests
{
    [Fact]
    public async Task Should_invoke_untyped_AndAlso_method_overloads()
    {
        // Arrange — int metadata composed with string metadata forces the untyped level
        AsyncSpecBase<object> asyncLeft = Spec.BuildAsync((object _) => new ValueTask<bool>(true))
            .WhenTrue(1).WhenFalse(0).Create("left");
        AsyncSpecBase<object> asyncRight = Spec.BuildAsync((object _) => new ValueTask<bool>(true))
            .WhenTrue("yes").WhenFalse("no").Create("right");
        SpecBase<object> syncRight = Spec.Build((object _) => true)
            .WhenTrue("yes").WhenFalse("no").Create("right");

        // Act
        AsyncSpecBase<object, string> asyncOperand = asyncLeft.AndAlso(asyncRight);
        AsyncSpecBase<object, string> syncOperand = asyncLeft.AndAlso(syncRight);

        // Assert
        (await asyncOperand.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await syncOperand.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_invoke_untyped_Or_method_overloads()
    {
        // Arrange
        AsyncSpecBase<object> asyncLeft = Spec.BuildAsync((object _) => new ValueTask<bool>(false))
            .WhenTrue(1).WhenFalse(0).Create("left");
        AsyncSpecBase<object> asyncRight = Spec.BuildAsync((object _) => new ValueTask<bool>(true))
            .WhenTrue("yes").WhenFalse("no").Create("right");
        SpecBase<object> syncRight = Spec.Build((object _) => true)
            .WhenTrue("yes").WhenFalse("no").Create("right");

        // Act
        AsyncSpecBase<object, string> asyncOperand = asyncLeft.Or(asyncRight);
        AsyncSpecBase<object, string> syncOperand = asyncLeft.Or(syncRight);

        // Assert
        (await asyncOperand.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await syncOperand.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_invoke_untyped_OrElse_method_overloads()
    {
        // Arrange
        AsyncSpecBase<object> asyncLeft = Spec.BuildAsync((object _) => new ValueTask<bool>(false))
            .WhenTrue(1).WhenFalse(0).Create("left");
        AsyncSpecBase<object> asyncRight = Spec.BuildAsync((object _) => new ValueTask<bool>(true))
            .WhenTrue("yes").WhenFalse("no").Create("right");
        SpecBase<object> syncRight = Spec.Build((object _) => true)
            .WhenTrue("yes").WhenFalse("no").Create("right");

        // Act
        AsyncSpecBase<object, string> asyncOperand = asyncLeft.OrElse(asyncRight);
        AsyncSpecBase<object, string> syncOperand = asyncLeft.OrElse(syncRight);

        // Assert
        (await asyncOperand.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await syncOperand.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_invoke_untyped_XOr_method_overloads()
    {
        // Arrange
        AsyncSpecBase<object> asyncLeft = Spec.BuildAsync((object _) => new ValueTask<bool>(true))
            .WhenTrue(1).WhenFalse(0).Create("left");
        AsyncSpecBase<object> asyncRight = Spec.BuildAsync((object _) => new ValueTask<bool>(false))
            .WhenTrue("yes").WhenFalse("no").Create("right");
        SpecBase<object> syncRight = Spec.Build((object _) => false)
            .WhenTrue("yes").WhenFalse("no").Create("right");

        // Act
        AsyncSpecBase<object, string> asyncOperand = asyncLeft.XOr(asyncRight);
        AsyncSpecBase<object, string> syncOperand = asyncLeft.XOr(syncRight);

        // Assert
        (await asyncOperand.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await syncOperand.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_invoke_untyped_pipe_operator_in_all_operand_directions()
    {
        // Arrange
        AsyncSpecBase<object> asyncLeft = Spec.BuildAsync((object _) => new ValueTask<bool>(false))
            .WhenTrue(1).WhenFalse(0).Create("asyncLeft");
        AsyncSpecBase<object> asyncRight = Spec.BuildAsync((object _) => new ValueTask<bool>(true))
            .WhenTrue("yes").WhenFalse("no").Create("asyncRight");
        SpecBase<object> syncLeft = Spec.Build((object _) => false)
            .WhenTrue(1).WhenFalse(0).Create("syncLeft");
        SpecBase<object> syncRight = Spec.Build((object _) => true)
            .WhenTrue("yes").WhenFalse("no").Create("syncRight");

        // Act — (async, async), (async, sync), (sync, async)
        AsyncSpecBase<object, string> asyncAsync = asyncLeft | asyncRight;
        AsyncSpecBase<object, string> asyncSync = asyncLeft | syncRight;
        AsyncSpecBase<object, string> syncAsync = syncLeft | asyncRight;

        // Assert
        (await asyncAsync.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await asyncSync.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await syncAsync.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_invoke_untyped_caret_operator_in_all_operand_directions()
    {
        // Arrange
        AsyncSpecBase<object> asyncLeft = Spec.BuildAsync((object _) => new ValueTask<bool>(true))
            .WhenTrue(1).WhenFalse(0).Create("asyncLeft");
        AsyncSpecBase<object> asyncRight = Spec.BuildAsync((object _) => new ValueTask<bool>(false))
            .WhenTrue("yes").WhenFalse("no").Create("asyncRight");
        SpecBase<object> syncLeft = Spec.Build((object _) => true)
            .WhenTrue(1).WhenFalse(0).Create("syncLeft");
        SpecBase<object> syncRight = Spec.Build((object _) => false)
            .WhenTrue("yes").WhenFalse("no").Create("syncRight");

        // Act — (async, async), (async, sync), (sync, async)
        AsyncSpecBase<object, string> asyncAsync = asyncLeft ^ asyncRight;
        AsyncSpecBase<object, string> asyncSync = asyncLeft ^ syncRight;
        AsyncSpecBase<object, string> syncAsync = syncLeft ^ asyncRight;

        // Assert
        (await asyncAsync.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await asyncSync.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await syncAsync.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_invoke_typed_mixed_pipe_and_caret_operators()
    {
        // Arrange — same metadata type on both sides so the typed overloads (not the untyped fallback) bind
        AsyncSpecBase<object, string> asyncLeft = Spec.BuildAsync((object _) => new ValueTask<bool>(false))
            .WhenTrue("yes").WhenFalse("no").Create("asyncLeft");
        SpecBase<object, string> syncRight = Spec.Build((object _) => true)
            .WhenTrue("yes").WhenFalse("no").Create("syncRight");

        // Act
        AsyncSpecBase<object, string> pipeResult = asyncLeft | syncRight;
        AsyncSpecBase<object, string> caretResult = syncRight ^ asyncLeft;

        // Assert
        (await pipeResult.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await caretResult.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
    }
}
