namespace Motiv.Tests;

public class ToAsyncSpecTests
{
    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_lift_a_sync_spec_with_full_result_parity(bool model)
    {
        // Arrange
        var sync = Spec.Build((bool b) => b)
            .WhenTrue("yes").WhenFalse("no").Create("decision");

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await sync.ToAsyncSpec().EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
        asyncResult.Values.ShouldBe(syncResult.Values);
    }

    [Fact]
    public void Should_cache_the_adapter_instance()
    {
        // Arrange
        var sync = Spec.Build((bool b) => b).Create("flag");

        // Act & Assert
        sync.ToAsyncSpec().ShouldBeSameAs(sync.ToAsyncSpec());
    }

    [Fact]
    public async Task Should_preserve_policy_ness_when_lifting_a_policy()
    {
        // Arrange
        PolicyBase<bool, string> policy = Spec.Build((bool b) => b).Create("flag");

        // Act
        AsyncPolicyBase<bool, string> lifted = policy.ToAsyncSpec();
        PolicyResultBase<string> result = await lifted.EvaluateAsync(true);

        // Assert
        result.Satisfied.ShouldBeTrue();
        lifted.ShouldBeSameAs(((SpecBase<bool, string>)policy).ToAsyncSpec());
    }

    [Fact]
    public void Should_forward_name_and_description()
    {
        // Arrange
        var sync = Spec.Build((bool b) => b).Create("flag");

        // Act & Assert
        sync.ToAsyncSpec().Name.ShouldBe(sync.Name);
        sync.ToAsyncSpec().Description.Statement.ShouldBe(sync.Description.Statement);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_match_via_the_boolean_only_path(bool model)
    {
        // Arrange
        var sync = Spec.Build((bool b) => b).Create("flag");

        // Act & Assert
        (await sync.ToAsyncSpec().MatchesAsync(model)).ShouldBe(sync.Matches(model));
    }

    [Fact]
    public async Task Should_surface_sync_predicate_exceptions_as_faulted_tasks()
    {
        // Arrange
        var sync = Spec.Build((bool _) => ThrowBoom()).Create("throws");

        // Act — the call itself must NOT throw; the returned task must be faulted
        var task = sync.ToAsyncSpec().EvaluateAsync(true);

        // Assert
        (await task.AsTask().ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe("boom");

        static bool ThrowBoom() => throw new InvalidOperationException("boom");
    }
}
