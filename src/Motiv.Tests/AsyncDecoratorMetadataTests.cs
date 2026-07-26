namespace Motiv.Tests;

public class AsyncDecoratorMetadataTests
{
    private sealed record Verdict(string Code)
    {
        // net472 cannot compile the positional init-only property (CS0518: IsExternalInit missing);
        // the get-only override mirrors AsyncMetadataPropositionTests.Message.
        public string Code { get; } = Code;
    }

    private static SpecBase<object, string> Sync(bool value) =>
        Spec.Build((object _) => value).Create("inner");

    private static AsyncSpecBase<object, string> Async(bool value) =>
        Spec.BuildAsync((object _) => new ValueTask<bool>(value)).Create("inner");

    [Theory]
    [InlineAutoData(true, "OK")]
    [InlineAutoData(false, "DENIED")]
    public async Task Should_remetadatize_an_async_spec_with_parity_to_sync(bool value, string expectedCode, object model)
    {
        // Arrange
        var sync = Spec.Build(Sync(value))
            .WhenTrue(new Verdict("OK")).WhenFalse(new Verdict("DENIED")).Create("verdict");
        var async = Spec.Build(Async(value))
            .WhenTrue(new Verdict("OK")).WhenFalse(new Verdict("DENIED")).Create("verdict");

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await async.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Values.ShouldBe([new Verdict(expectedCode)]);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);   // ["verdict == true|false"]
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Theory]
    [InlineAutoData(true, "OK")]
    [InlineAutoData(false, "DENIED")]
    public async Task Should_remetadatize_an_async_spec_with_non_string_underlying_metadata(bool value, string expectedCode, object model)
    {
        // Arrange — the inner specs carry int metadata, exercising the generic-underlying factory arity
        var sync = Spec.Build(Spec.Build((object _) => value).WhenTrue(1).WhenFalse(0).Create("inner"))
            .WhenTrue(new Verdict("OK")).WhenFalse(new Verdict("DENIED")).Create("verdict");
        var async = Spec.Build(Spec.BuildAsync((object _) => new ValueTask<bool>(value)).WhenTrue(1).WhenFalse(0).Create("inner"))
            .WhenTrue(new Verdict("OK")).WhenFalse(new Verdict("DENIED")).Create("verdict");

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await async.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Values.ShouldBe([new Verdict(expectedCode)]);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_support_model_function_metadata_payloads(bool value, object model)
    {
        // Arrange
        var sync = Spec.Build(Sync(value))
            .WhenTrue(_ => new Verdict("OK"))
            .WhenFalse(_ => new Verdict("DENIED"))
            .Create("verdict");
        var async = Spec.Build(Async(value))
            .WhenTrue(_ => new Verdict("OK"))
            .WhenFalse(_ => new Verdict("DENIED"))
            .Create("verdict");

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await async.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Values.ShouldBe(syncResult.Values);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public async Task Should_support_result_consuming_metadata_payloads(bool value, object model)
    {
        // Arrange — payload factories consume the underlying result
        var sync = Spec.Build(Sync(value))
            .WhenTrue((_, r) => new Verdict($"OK: {r.Reason}"))
            .WhenFalse((_, r) => new Verdict($"DENIED: {r.Reason}"))
            .Create("verdict");
        var async = Spec.Build(Async(value))
            .WhenTrue((_, r) => new Verdict($"OK: {r.Reason}"))
            .WhenFalse((_, r) => new Verdict($"DENIED: {r.Reason}"))
            .Create("verdict");

        // Act
        var syncResult = sync.Evaluate(model);
        var asyncResult = await async.EvaluateAsync(model);

        // Assert
        asyncResult.Satisfied.ShouldBe(syncResult.Satisfied);
        asyncResult.Values.ShouldBe(syncResult.Values);
        if (!value)
            asyncResult.Values.ShouldBe([new Verdict("DENIED: inner == false")]);
        asyncResult.Assertions.ShouldBe(syncResult.Assertions);
        asyncResult.Reason.ShouldBe(syncResult.Reason);
        asyncResult.Justification.ShouldBe(syncResult.Justification);
    }

    [Fact]
    public async Task Should_create_an_async_policy_yielding_the_single_metadata_value()
    {
        // Arrange
        var inner = Spec.BuildAsync((object _) => new ValueTask<bool>(true)).Create("inner");

        // Act
        var decorated = Spec.Build(inner).WhenTrue(new Verdict("OK")).WhenFalse(new Verdict("DENIED")).Create("verdict");

        // Assert — Value only exists on PolicyResultBase, proving policy preservation statically
        (await decorated.EvaluateAsync(new object())).Value.ShouldBe(new Verdict("OK"));
    }

    [Fact]
    public async Task Should_yield_the_false_metadata_value_when_unsatisfied()
    {
        // Arrange
        var inner = Spec.BuildAsync((object _) => new ValueTask<bool>(false)).Create("inner");

        // Act
        var decorated = Spec.Build(inner).WhenTrue(new Verdict("OK")).WhenFalse(new Verdict("DENIED")).Create("verdict");

        // Assert
        (await decorated.EvaluateAsync(new object())).Value.ShouldBe(new Verdict("DENIED"));
    }

    [Fact]
    public void Should_throw_when_create_statement_is_whitespace()
    {
        // Act — mirrors the sync factory's ThrowIfNullOrWhitespace guard on the statement
        var act = () => Spec.Build(Async(true)).WhenTrue(new Verdict("OK")).WhenFalse(new Verdict("DENIED")).Create(" ");

        // Assert
        act.ShouldThrow<ArgumentException>();
    }
}
