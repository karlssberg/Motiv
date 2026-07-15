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
}
