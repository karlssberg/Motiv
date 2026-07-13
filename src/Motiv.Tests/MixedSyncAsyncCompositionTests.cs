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
        // Arrange
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
