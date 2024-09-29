using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv;

/// <summary>
/// Provides extension methods for assertions.
/// </summary>
public static class AssertionExtensions
{
    /// <summary>
    /// Gets the assertions from a collection of boolean results.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to get assertions from.</param>
    /// <returns>A collection of assertions from the boolean results.</returns>
    public static IEnumerable<string> GetAssertions(
        this IEnumerable<BooleanResultBase> results) =>
        results
            .SelectMany(result =>
                result switch
                {
                    IBooleanOperationResult operationResult => operationResult.Causes.GetAssertions(),
                    _ => result.Assertions
                });


    /// <summary>
    /// Gets the assertions from a collection of boolean results.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to get all assertions from.</param>
    /// <returns>A collection of all assertions yielded during the creation of the boolean results.</returns>
    public static IEnumerable<string> GetAllAssertions(
        this IEnumerable<BooleanResultBase> results) =>
        results
            .SelectMany(result =>
                result switch
                {
                    IBooleanOperationResult operationResult => operationResult.Underlying.GetAllAssertions(),
                    _ => result.AllAssertions
                });

    /// <summary>
    /// Get the assertions from a collection of boolean results that are true.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to get assertions from.</param>
    /// <returns>A collection of assertions from the boolean results that are true.</returns>
    public static IEnumerable<string> GetTrueAssertions(
        this IEnumerable<BooleanResultBase> results) =>
        results
            .Where(r => r.Satisfied)
            .SelectMany(e => e.Assertions);

    /// <summary>
    /// Get the assertions from a collection of boolean results that are false.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to get assertions from.</param>
    /// <returns>A collection of assertions from the boolean results that are false.</returns>
    public static IEnumerable<string> GetFalseAssertions(
        this IEnumerable<BooleanResultBase> results) =>
        results
            .Where(r => !r.Satisfied)
            .SelectMany(e => e.Assertions);

    /// <summary>
    /// Get the assertions from the root causes of a boolean result, instead of causes from possible intermediate
    /// propositions.
    /// </summary>
    /// <param name="result">The boolean result to get the root assertions from.</param>
    /// <returns>A collection of assertions from the root causes of the boolean result.</returns>
    public static IEnumerable<string> GetRootAssertions(
        this BooleanResultBase result) =>
        result.Explanation
            .Underlying
            .GetRootAssertions()
            .DistinctWithOrderPreserved()
            .ElseIfEmpty(result.Assertions);

    /// <summary>
    /// Get the assertions from the root causes of a boolean result, instead of causes from possible intermediate
    /// propositions.
    /// </summary>
    /// <param name="result">The boolean result to get the root assertions from.</param>
    /// <returns>A collection of assertions from the root causes of the boolean result.</returns>
    public static IEnumerable<string> GetAllRootAssertions(
        this BooleanResultBase result) =>
        result.Underlying
            .GetAllRootAssertions()
            .DistinctWithOrderPreserved()
            .ElseIfEmpty(result.Assertions);

    private static IEnumerable<string> GetRootAssertions(
        this IEnumerable<Explanation> explanations) =>
        explanations.SelectMany(explanation => explanation
            .Underlying
            .GetRootAssertions()
            .ElseIfEmpty(explanation.Assertions));

    private static IEnumerable<string> GetAllRootAssertions(
        this IEnumerable<BooleanResultBase> results) =>
        results.SelectMany(result => result
            .GetAllRootAssertions()
            .ElseIfEmpty(result.Assertions));
}
