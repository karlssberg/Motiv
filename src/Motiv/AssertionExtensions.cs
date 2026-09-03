using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv;

/// <summary>
/// Provides extension methods for assertions.
/// </summary>
/// <remarks>
/// These walks fold over nodes they do not own — a caller's sequence, or a result's explanation — so
/// they have no node field to memoise into and use a walk-local memo instead. They were the last
/// members standing before Spec 3A, at a ceiling of roughly a thousand operands, and — being lazy and
/// un-memoised — they re-allocated their whole iterator chain on every enumeration.
/// </remarks>
public static class AssertionExtensions
{
    /// <summary>
    /// Gets the assertions from a collection of boolean results.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to get assertions from.</param>
    /// <returns>A collection of assertions from the boolean results.</returns>
    public static IEnumerable<string> GetAssertions(
        this IEnumerable<BooleanResultBase> results)
    {
        // Deferred: Explanation's constructor calls this speculatively for every node it builds, so
        // folding at call time would make evaluating a composition quadratic in its size.
        foreach (var assertion in FoldEach(results, CausalOperands, CombineAssertions))
            yield return assertion;
    }

    /// <summary>
    /// Gets the assertions from a collection of boolean results.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to get all assertions from.</param>
    /// <returns>A collection of all assertions yielded during the creation of the boolean results.</returns>
    public static IEnumerable<string> GetAllAssertions(
        this IEnumerable<BooleanResultBase> results)
    {
        foreach (var assertion in FoldEach(results, UnderlyingOperands, CombineAllAssertions))
            yield return assertion;
    }

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
    /// <remarks>
    /// The deepest explanation is a property of a branch, so this descends an explanation's
    /// un-collapsed branches rather than <see cref="Explanation.Underlying" /> — see that property's
    /// remarks for why the latter cannot answer the question (ticket #192). It walks from the result's
    /// own explanation rather than from the level below it, which is what makes the walk's per-branch
    /// fallback the root's fallback too; the <c>ElseIfEmpty</c> that used to supply that at the root
    /// fired only when the <i>whole</i> level came out empty, and so could not tell a level that lost
    /// one operand from one that kept them all.
    /// </remarks>
    public static IEnumerable<string> GetRootAssertions(
        this BooleanResultBase result)
    {
        var rootAssertions = FoldEach(result.Explanation.ToEnumerable(), ExplanationBranches, CombineRootAssertions)
            .DistinctWithOrderPreserved();

        foreach (var assertion in rootAssertions)
            yield return assertion;
    }

    /// <summary>
    /// Get the assertions from the root causes of a boolean result, instead of causes from possible intermediate
    /// propositions.
    /// </summary>
    /// <param name="result">The boolean result to get the root assertions from.</param>
    /// <returns>A collection of assertions from the root causes of the boolean result.</returns>
    public static IEnumerable<string> GetAllRootAssertions(
        this BooleanResultBase result)
    {
        foreach (var assertion in FoldEach(result.ToEnumerable(), AllOperands, CombineAllRootAssertions))
            yield return assertion;
    }

    private static readonly Func<BooleanResultBase, IReadOnlyList<BooleanResultBase>> CausalOperands =
        result => result is IBooleanOperationResult operation ? operation.Causes.AsList() : [];

    private static readonly Func<BooleanResultBase, IReadOnlyList<BooleanResultBase>> UnderlyingOperands =
        result => result is IBooleanOperationResult operation ? operation.Underlying.AsList() : [];

    private static readonly Func<BooleanResultBase, IReadOnlyList<BooleanResultBase>> AllOperands =
        result => result.Underlying.AsList();

    private static readonly Func<Explanation, IReadOnlyList<Explanation>> ExplanationBranches =
        explanation => explanation.Branches;

    private static readonly Func<BooleanResultBase, IReadOnlyList<string[]>, string[]> CombineAssertions =
        (result, foldedCauses) => result is IBooleanOperationResult
            ? foldedCauses.Flatten()
            : AsArray(result.Explanation.Assertions);

    private static readonly Func<BooleanResultBase, IReadOnlyList<string[]>, string[]> CombineAllAssertions =
        (result, foldedUnderlying) => result is IBooleanOperationResult
            ? foldedUnderlying.Flatten()
            : AsArray(result.Explanation.AllAssertions);

    private static readonly Func<Explanation, IReadOnlyList<string[]>, string[]> CombineRootAssertions =
        (explanation, foldedBranches) =>
        {
            var rootAssertions = foldedBranches.Flatten();

            return rootAssertions.Length == 0
                ? AsArray(explanation.Assertions)
                : rootAssertions;
        };

    /// <remarks>
    /// The public <c>GetAllRootAssertions</c> de-duplicates and falls back at <i>every</i> level, not
    /// only at the root, because its private helper recurses back through the public method.
    /// </remarks>
    private static readonly Func<BooleanResultBase, IReadOnlyList<string[]>, string[]> CombineAllRootAssertions =
        (result, foldedUnderlying) =>
        {
            var rootAssertions = new List<string>();
            var next = 0;

            foreach (var underlying in result.Underlying)
            {
                var fromUnderlying = foldedUnderlying[next++];
                rootAssertions.AddRange(fromUnderlying.Length == 0 ? AsArray(underlying.Assertions) : fromUnderlying);
            }

            return rootAssertions.Count == 0
                ? AsArray(result.Assertions)
                : rootAssertions.DistinctWithOrderPreserved().ToArray();
        };

    private static IEnumerable<string> FoldEach<TNode>(
        IEnumerable<TNode> roots,
        Func<TNode, IReadOnlyList<TNode>> descend,
        Func<TNode, IReadOnlyList<string[]>, string[]> combine)
        where TNode : class
    {
        var memo = new Dictionary<TNode, string[]>(ReferenceEqualityComparer<TNode>.Instance);
        var assertions = new List<string>();

        foreach (var root in roots)
            assertions.AddRange(PostOrderFold.Fold(
                root,
                descend,
                combine,
                node => memo.TryGetValue(node, out var folded) ? folded : null,
                (node, folded) => memo[node] = folded));

        return assertions;
    }

    private static string[] AsArray(IEnumerable<string> assertions) =>
        assertions as string[] ?? assertions.ToArray();
}
