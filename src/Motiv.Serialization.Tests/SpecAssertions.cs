namespace Motiv.Serialization.Tests;

internal static class SpecAssertions
{
    /// <summary>
    /// Asserts that a loaded spec evaluates identically to its expected fluent-built equivalent
    /// across the given models, comparing outcome, reason, assertions and justification.
    /// </summary>
    public static void ShouldBehaveIdentically<TModel>(
        SpecBase<TModel, string> loaded,
        SpecBase<TModel, string> expected,
        params TModel[] models)
    {
        foreach (var model in models)
        {
            var expectedResult = expected.Evaluate(model);
            var actualResult = loaded.Evaluate(model);
            actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
            actualResult.Reason.ShouldBe(expectedResult.Reason);
            actualResult.Assertions.ShouldBe(expectedResult.Assertions);
            actualResult.Justification.ShouldBe(expectedResult.Justification);
        }
    }

    /// <summary>
    /// Asserts that a loaded async spec evaluates identically to its expected fluent-built async
    /// equivalent across the given models, comparing outcome, reason, assertions and justification.
    /// </summary>
    public static async Task ShouldBehaveIdenticallyAsync<TModel>(
        AsyncSpecBase<TModel, string> loaded,
        AsyncSpecBase<TModel, string> expected,
        params TModel[] models)
    {
        foreach (var model in models)
        {
            var expectedResult = await expected.EvaluateAsync(model);
            var actualResult = await loaded.EvaluateAsync(model);
            actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
            actualResult.Reason.ShouldBe(expectedResult.Reason);
            actualResult.Assertions.ShouldBe(expectedResult.Assertions);
            actualResult.Justification.ShouldBe(expectedResult.Justification);
        }
    }

    /// <summary>
    /// Asserts that a loaded async spec evaluates identically to a synchronous expected spec
    /// (e.g. the sync load of the same fully-sync document), verifying the lift is
    /// justification-preserving.
    /// </summary>
    public static async Task ShouldBehaveIdenticallyAsync<TModel>(
        AsyncSpecBase<TModel, string> loaded,
        SpecBase<TModel, string> expected,
        params TModel[] models)
    {
        foreach (var model in models)
        {
            var expectedResult = expected.Evaluate(model);
            var actualResult = await loaded.EvaluateAsync(model);
            actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
            actualResult.Reason.ShouldBe(expectedResult.Reason);
            actualResult.Assertions.ShouldBe(expectedResult.Assertions);
            actualResult.Justification.ShouldBe(expectedResult.Justification);
        }
    }

    /// <summary>
    /// Asserts that a loaded async metadata spec evaluates identically to its expected
    /// fluent-built async equivalent, additionally comparing the metadata values surfaced by the
    /// evaluation.
    /// </summary>
    public static async Task ShouldBehaveIdenticallyAsync<TModel, TMetadata>(
        AsyncSpecBase<TModel, TMetadata> loaded,
        AsyncSpecBase<TModel, TMetadata> expected,
        params TModel[] models)
    {
        foreach (var model in models)
        {
            var expectedResult = await expected.EvaluateAsync(model);
            var actualResult = await loaded.EvaluateAsync(model);
            actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
            actualResult.Reason.ShouldBe(expectedResult.Reason);
            actualResult.Assertions.ShouldBe(expectedResult.Assertions);
            actualResult.Justification.ShouldBe(expectedResult.Justification);
            actualResult.Values.ShouldBe(expectedResult.Values);
        }
    }

    /// <summary>
    /// Asserts that a loaded async metadata spec evaluates identically to a synchronous expected
    /// spec (e.g. the sync metadata load of the same fully-sync document), verifying the lift is
    /// justification- and value-preserving.
    /// </summary>
    public static async Task ShouldBehaveIdenticallyAsync<TModel, TMetadata>(
        AsyncSpecBase<TModel, TMetadata> loaded,
        SpecBase<TModel, TMetadata> expected,
        params TModel[] models)
    {
        foreach (var model in models)
        {
            var expectedResult = expected.Evaluate(model);
            var actualResult = await loaded.EvaluateAsync(model);
            actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
            actualResult.Reason.ShouldBe(expectedResult.Reason);
            actualResult.Assertions.ShouldBe(expectedResult.Assertions);
            actualResult.Justification.ShouldBe(expectedResult.Justification);
            actualResult.Values.ShouldBe(expectedResult.Values);
        }
    }

    /// <summary>
    /// Asserts that a loaded metadata spec evaluates identically to its expected fluent-built
    /// equivalent, additionally comparing the metadata values surfaced by the evaluation.
    /// </summary>
    public static void ShouldBehaveIdentically<TModel, TMetadata>(
        SpecBase<TModel, TMetadata> loaded,
        SpecBase<TModel, TMetadata> expected,
        params TModel[] models)
    {
        foreach (var model in models)
        {
            var expectedResult = expected.Evaluate(model);
            var actualResult = loaded.Evaluate(model);
            actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
            actualResult.Reason.ShouldBe(expectedResult.Reason);
            actualResult.Assertions.ShouldBe(expectedResult.Assertions);
            actualResult.Justification.ShouldBe(expectedResult.Justification);
            actualResult.Values.ShouldBe(expectedResult.Values);
        }
    }
}
