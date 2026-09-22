namespace Motiv.Tests;

public class AsyncSyncEquivalenceMatrixTests
{
    public enum Op { And, AndAlso, Or, OrElse, XOr, AndConcurrently, OrConcurrently, XOrConcurrently }

    public static TheoryData<Op, bool, bool> Cases()
    {
        var data = new TheoryData<Op, bool, bool>();
        foreach (var op in (Op[])Enum.GetValues(typeof(Op)))
        foreach (var left in new[] { true, false })
        foreach (var right in new[] { true, false })
            data.Add(op, left, right);
        return data;
    }

    private static SpecBase<object, string> Compose(
        Op op, SpecBase<object, string> l, SpecBase<object, string> r) =>
        op switch
        {
            Op.And or Op.AndConcurrently => l.And(r),
            Op.AndAlso => l.AndAlso(r),
            Op.Or or Op.OrConcurrently => l.Or(r),
            Op.OrElse => l.OrElse(r),
            Op.XOr or Op.XOrConcurrently => l.XOr(r),
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

    private static AsyncSpecBase<object, string> ComposeAsync(
        Op op, AsyncSpecBase<object, string> l, AsyncSpecBase<object, string> r) =>
        op switch
        {
            Op.And => l.And(r),
            Op.AndAlso => l.AndAlso(r),
            Op.Or => l.Or(r),
            Op.OrElse => l.OrElse(r),
            Op.XOr => l.XOr(r),
            Op.AndConcurrently => l.AndConcurrently(r),
            Op.OrConcurrently => l.OrConcurrently(r),
            Op.XOrConcurrently => l.XOrConcurrently(r),
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Async_compositions_are_equivalent_to_sync(Op op, bool leftValue, bool rightValue)
    {
        // Arrange
        var syncSpec = Compose(op,
            Spec.Build((object _) => leftValue).WhenTrue("lt").WhenFalse("lf").Create("left"),
            Spec.Build((object _) => rightValue).WhenTrue("rt").WhenFalse("rf").Create("right"));
        var asyncSpec = ComposeAsync(op,
            Spec.BuildAsync((object _) => new ValueTask<bool>(leftValue)).WhenTrue("lt").WhenFalse("lf").Create("left"),
            Spec.BuildAsync((object _) => new ValueTask<bool>(rightValue)).WhenTrue("rt").WhenFalse("rf").Create("right"));
        var liftedSpec = ComposeAsync(op,
            Spec.Build((object _) => leftValue).WhenTrue("lt").WhenFalse("lf").Create("left").ToAsyncSpec(),
            Spec.Build((object _) => rightValue).WhenTrue("rt").WhenFalse("rf").Create("right").ToAsyncSpec());

        var model = new object();

        // Act
        var expected = syncSpec.Evaluate(model);
        var fromAsync = await asyncSpec.EvaluateAsync(model);
        var fromLifted = await liftedSpec.EvaluateAsync(model);
        var matches = await asyncSpec.MatchesAsync(model);

        // Assert — every observable surface agrees
        foreach (var actual in new[] { fromAsync, fromLifted })
        {
            actual.Satisfied.ShouldBe(expected.Satisfied);
            actual.Reason.ShouldBe(expected.Reason);
            actual.Assertions.ShouldBe(expected.Assertions);
            actual.Justification.ShouldBe(expected.Justification);
            actual.Values.ShouldBe(expected.Values);
        }
        matches.ShouldBe(expected.Satisfied);
        asyncSpec.Description.Statement.ShouldBe(syncSpec.Description.Statement);
        liftedSpec.Description.Statement.ShouldBe(syncSpec.Description.Statement);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Async_negation_is_equivalent_to_sync(bool value)
    {
        // Arrange
        var syncSpec = !Spec.Build((object _) => value).Create("flag");
        var asyncSpec = !Spec.BuildAsync((object _) => new ValueTask<bool>(value)).Create("flag");
        var model = new object();

        // Act
        var expected = syncSpec.Evaluate(model);
        var actual = await asyncSpec.EvaluateAsync(model);

        // Assert
        actual.Satisfied.ShouldBe(expected.Satisfied);
        actual.Reason.ShouldBe(expected.Reason);
        actual.Justification.ShouldBe(expected.Justification);
    }
}
