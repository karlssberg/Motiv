using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv;

/// <summary>
/// Provides extension methods for assertions.
/// </summary>
/// <remarks>
/// These four walks take an arbitrary sequence rather than a single result, so they have no node
/// field to memoise into and use a walk-local memo instead. They were the last members standing
/// before Spec 3A, at a ceiling of roughly a thousand operands, and — being lazy and un-memoised —
/// they re-allocated their whole iterator chain on every enumeration.
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
    public static IEnumerable<string> GetRootAssertions(
        this BooleanResultBase result)
    {
        var rootAssertions = FoldEach(result.Explanation.Underlying, ExplanationUnderlying, CombineRootAssertions)
            .DistinctWithOrderPreserved()
            .ElseIfEmpty(result.Assertions);

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
        result => result is IBooleanOperationResult operation ? AsList(operation.Causes) : [];

    private static readonly Func<BooleanResultBase, IReadOnlyList<BooleanResultBase>> UnderlyingOperands =
        result => result is IBooleanOperationResult operation ? AsList(operation.Underlying) : [];

    private static readonly Func<BooleanResultBase, IReadOnlyList<BooleanResultBase>> AllOperands =
        result => AsList(result.Underlying);

    private static readonly Func<Explanation, IReadOnlyList<Explanation>> ExplanationUnderlying =
        explanation => AsList(explanation.Underlying);

    private static readonly Func<BooleanResultBase, IReadOnlyList<string[]>, string[]> CombineAssertions =
        (result, foldedCauses) => result is IBooleanOperationResult
            ? Flatten(foldedCauses)
            : AsArray(result.Explanation.Assertions);

    private static readonly Func<BooleanResultBase, IReadOnlyList<string[]>, string[]> CombineAllAssertions =
        (result, foldedUnderlying) => result is IBooleanOperationResult
            ? Flatten(foldedUnderlying)
            : AsArray(result.Explanation.AllAssertions);

    private static readonly Func<Explanation, IReadOnlyList<string[]>, string[]> CombineRootAssertions =
        (explanation, foldedUnderlying) =>
        {
            var rootAssertions = Flatten(foldedUnderlying);

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

    private static string[] Flatten(IReadOnlyList<string[]> blocks)
    {
        var total = 0;
        for (var i = 0; i < blocks.Count; i++)
            total += blocks[i].Length;

        if (total == 0)
            return [];

        var flattened = new string[total];
        var next = 0;
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            Array.Copy(block, 0, flattened, next, block.Length);
            next += block.Length;
        }

        return flattened;
    }

    private static string[] AsArray(IEnumerable<string> assertions) =>
        assertions as string[] ?? assertions.ToArray();

    private static IReadOnlyList<T> AsList<T>(IEnumerable<T> items) =>
        items as IReadOnlyList<T> ?? items.ToArray();
}
