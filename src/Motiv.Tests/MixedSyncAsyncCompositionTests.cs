namespace Motiv.Tests;

public class MixedSyncAsyncCompositionTests
{
    [Theory]
    [InlineAutoData(true, true, true)]
    [InlineAutoData(true, false, false)]
    [InlineAutoData(false, true, false)]
    public async Task Should_compose_sync_and_also_async_like_the_spec_headline(
        bool isAdultValue, bool hasCreditValue, bool expected, object model)
    {
        // Arrange — the design-spec headline example shape
        var isAdult = Spec.Build((object _) => isAdultValue).Create("is adult");
        var hasCredit = Spec.BuildAsync((object _) => Task.FromResult(hasCreditValue)).Create("has credit");

        // Act
        var canBuy = isAdult.AndAlso(hasCredit);          // AsyncSpecBase
        var result = await canBuy.EvaluateAsync(model);

        // Assert
        result.Satisfied.ShouldBe(expected);
    }

    [Fact]
    public async Task Should_not_start_async_work_when_the_sync_left_short_circuits()
    {
        // Arrange
        var apiCalls = 0;
        var isAdult = Spec.Build((object _) => false).Create("is adult");
        var hasCredit = Spec.BuildAsync((object _) =>
        {
            apiCalls++;
            return Task.FromResult(true);
        }).Create("has credit");

        // Act
        var result = await isAdult.AndAlso(hasCredit).EvaluateAsync(new object());

        // Assert
        result.Satisfied.ShouldBeFalse();
        apiCalls.ShouldBe(0);
    }

    [Theory]
    [InlineAutoData(true, true)]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public async Task Should_render_mixed_operator_compositions_with_all_sync_parity(
        bool leftValue, bool rightValue, object model)
    {
        // Arrange — minimal propositions, unlike the named explanation propositions used by the matrix theory
        // below (which have different Values semantics)
        var syncLeft = Spec.Build((object _) => leftValue).Create("left");
        var syncRight = Spec.Build((object _) => rightValue).Create("right");
        var asyncRight = Spec.BuildAsync((object _) => Task.FromResult(rightValue)).Create("right");

        // Act
        var allSync = (syncLeft & syncRight).Evaluate(model);
        var mixed = await (syncLeft & asyncRight).EvaluateAsync(model);

        // Assert
        mixed.Satisfied.ShouldBe(allSync.Satisfied);
        mixed.Reason.ShouldBe(allSync.Reason);
        mixed.Assertions.ShouldBe(allSync.Assertions);
        mixed.Justification.ShouldBe(allSync.Justification);
    }

    [Fact]
    public async Task Should_preserve_policy_ness_across_mixed_or_else()
    {
        // Arrange
        PolicyBase<object, string> syncPolicy = Spec.Build((object _) => false).Create("primary");
        AsyncPolicyBase<object, string> asyncPolicy =
            Spec.BuildAsync((object _) => Task.FromResult(true)).Create("fallback");

        // Act — both directions must compile as policies
        AsyncPolicyBase<object, string> a = syncPolicy.OrElse(asyncPolicy);
        AsyncPolicyBase<object, string> b = asyncPolicy.OrElse(syncPolicy);

        // Assert
        (await a.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await b.EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
    }

    public enum MixedOp
    {
        And, AndAlso, Or, OrElse, XOr,
        AndConcurrently, OrConcurrently, XOrConcurrently,
        BitwiseAnd, BitwiseOr, BitwiseXOr
    }

    public static TheoryData<MixedOp, bool, bool> MixedCases()
    {
        var data = new TheoryData<MixedOp, bool, bool>();
        // Enum.GetValues<MixedOp>() is .NET 5+, and this assembly still targets net472.
        foreach (var op in (MixedOp[])Enum.GetValues(typeof(MixedOp)))
        foreach (var left in new[] { true, false })
        foreach (var right in new[] { true, false })
            data.Add(op, left, right);
        return data;
    }

    // The Concurrently variants differ from their sequential counterparts only in evaluation strategy — they
    // must render identically — so they map onto the same all-sync composition here.
    private static SpecBase<object, string> ComposeSync(
        MixedOp op, SpecBase<object, string> left, SpecBase<object, string> right) =>
        op switch
        {
            MixedOp.And or MixedOp.AndConcurrently or MixedOp.BitwiseAnd => left.And(right),
            MixedOp.AndAlso => left.AndAlso(right),
            MixedOp.Or or MixedOp.OrConcurrently or MixedOp.BitwiseOr => left.Or(right),
            MixedOp.OrElse => left.OrElse(right),
            MixedOp.XOr or MixedOp.XOrConcurrently or MixedOp.BitwiseXOr => left.XOr(right),
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

    // The operator forms are a distinct binding surface from the method forms, so both are exercised.
    private static AsyncSpecBase<object, string> ComposeMixed(
        MixedOp op, SpecBase<object, string> left, AsyncSpecBase<object, string> right) =>
        op switch
        {
            MixedOp.And => left.And(right),
            MixedOp.AndAlso => left.AndAlso(right),
            MixedOp.Or => left.Or(right),
            MixedOp.OrElse => left.OrElse(right),
            MixedOp.XOr => left.XOr(right),
            MixedOp.AndConcurrently => left.AndConcurrently(right),
            MixedOp.OrConcurrently => left.OrConcurrently(right),
            MixedOp.XOrConcurrently => left.XOrConcurrently(right),
            MixedOp.BitwiseAnd => left & right,
            MixedOp.BitwiseOr => left | right,
            MixedOp.BitwiseXOr => left ^ right,
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

    [Theory]
    [MemberData(nameof(MixedCases))]
    public async Task Should_render_a_mixed_composition_identically_to_its_all_sync_equivalent(
        MixedOp op, bool leftValue, bool rightValue)
    {
        // Arrange — statically typed as SpecBase so the sync-side mixed-mode overloads bind
        SpecBase<object, string> syncLeft =
            Spec.Build((object _) => leftValue).WhenTrue("lt").WhenFalse("lf").Create("left");
        SpecBase<object, string> syncRight =
            Spec.Build((object _) => rightValue).WhenTrue("rt").WhenFalse("rf").Create("right");
        AsyncSpecBase<object, string> asyncRight =
            Spec.BuildAsync((object _) => Task.FromResult(rightValue)).WhenTrue("rt").WhenFalse("rf").Create("right");

        var allSync = ComposeSync(op, syncLeft, syncRight);
        var mixed = ComposeMixed(op, syncLeft, asyncRight);
        var model = new object();

        // Act
        var expected = allSync.Evaluate(model);
        var actual = await mixed.EvaluateAsync(model);

        // Assert
        actual.Satisfied.ShouldBe(expected.Satisfied);
        actual.Reason.ShouldBe(expected.Reason);
        actual.Assertions.ShouldBe(expected.Assertions);
        actual.Justification.ShouldBe(expected.Justification);
        actual.Values.ShouldBe(expected.Values);
        mixed.Description.Statement.ShouldBe(allSync.Description.Statement);
        (await mixed.MatchesAsync(model)).ShouldBe(expected.Satisfied);
    }

    [Fact]
    public async Task Should_lift_sync_operand_in_both_operator_directions()
    {
        // Arrange
        var sync = Spec.Build((object _) => true).Create("sync");
        var async = Spec.BuildAsync((object _) => Task.FromResult(true)).Create("async");

        // Act & Assert — compile-time direction checks + evaluation sanity
        (await (sync & async).EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await (async & sync).EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await (sync | async).EvaluateAsync(new object())).Satisfied.ShouldBeTrue();
        (await (async ^ sync).EvaluateAsync(new object())).Satisfied.ShouldBeFalse();
    }
}
