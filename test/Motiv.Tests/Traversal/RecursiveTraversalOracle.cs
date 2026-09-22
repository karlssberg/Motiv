using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.Tests.Traversal;

/// <summary>
/// A verbatim copy of the recursive result-tree and description-tree walks as they stood before
/// Spec 3A replaced them with <see cref="PostOrderFold" />. Held in the test project so that the
/// differential comparison stays runnable in CI forever: a later change that quietly alters traversal
/// semantics fails a test rather than passing review.
/// </summary>
/// <remarks>
/// These copies are deliberately un-memoised. They are only ever run over trees shallow enough that
/// recursion does not overflow, which is what makes them a usable oracle.
/// <para>
/// The three source walks are no longer verbatim. <see cref="UnderlyingMetadataSources{TMetadata}" />
/// was settled as defective by ticket #136, and all three lost their fallback-to-self by ticket #188.
/// <see cref="GetRootAssertions" /> joined them at ticket #192, which settled its level-wide fallback
/// as the defect. For those the oracle's claim is weaker than for the rest of this class: not "does
/// the fold match what shipped before Spec 3A?" but only "does the fold match an independent
/// recursive formulation?". #188's and #192's own tests carry the behavioural claim the oracle can no
/// longer make.
/// </para>
/// </remarks>
internal static class RecursiveTraversalOracle
{
    /// <remarks>
    /// Not verbatim: the trailing <c>ElseIfEmpty(result.ToEnumerable())</c> was removed by ticket
    /// #188, which settled that a result with no causes has no sources rather than being its own.
    /// </remarks>
    internal static IEnumerable<BooleanResultBase> UnderlyingAssertionSources(BooleanResultBase result) =>
        result.Causes
            .SelectMany(booleanResult =>
                booleanResult is IBooleanOperationResult
                    ? UnderlyingAssertionSources(booleanResult)
                    : booleanResult.ToEnumerable());

    /// <inheritdoc cref="UnderlyingAssertionSources" />
    internal static IEnumerable<BooleanResultBase> UnderlyingAllAssertionSources(BooleanResultBase result) =>
        result.Underlying
            .SelectMany(booleanResult =>
                booleanResult is IBooleanOperationResult
                    ? UnderlyingAllAssertionSources(booleanResult)
                    : booleanResult.ToEnumerable());

    /// <remarks>
    /// Not a verbatim copy, twice over. As it stood this yielded <c>result</c> where its siblings
    /// yield <c>booleanResult</c>, which ticket #136 settled as a copy-paste slip; and the fallback
    /// #136 gave it to match its siblings was then removed from all three by ticket #188.
    /// </remarks>
    internal static IEnumerable<BooleanResultBase<TMetadata>> UnderlyingMetadataSources<TMetadata>(
        BooleanResultBase<TMetadata> result) =>
        result.CausesWithValues
            .SelectMany(booleanResult =>
                booleanResult is IBooleanOperationResult
                    ? UnderlyingMetadataSources(booleanResult)
                    : booleanResult.ToEnumerable());

    internal static IEnumerable<BooleanResultBase> UnderlyingExpressionResults(BooleanResultBase result) =>
        result.Causes
            .SelectMany(booleanResult =>
                (result, booleanResult) switch
                {
                    (not IBooleanOperationResult, IBooleanOperationResult) =>
                        booleanResult.ToEnumerable().Concat(UnderlyingExpressionResults(booleanResult)),
                    _ => UnderlyingExpressionResults(booleanResult),
                });

    internal static IEnumerable<string> UnderlyingReasons(BooleanResultBase result) =>
        UnderlyingExpressionResults(result).Select(underlying => underlying.Reason);

    internal static IEnumerable<string> AllAssertions(BooleanResultBase result) =>
        result switch
        {
            IBinaryBooleanOperationResult binary => binary.Underlying.SelectMany(AllAssertions),
            _ => result.Assertions
        };

    internal static IEnumerable<string> GetAssertions(IEnumerable<BooleanResultBase> results) =>
        results
            .SelectMany(result =>
                result switch
                {
                    IBooleanOperationResult operationResult => GetAssertions(operationResult.Causes),
                    _ => result.Explanation.Assertions
                });

    internal static IEnumerable<string> GetAllAssertions(IEnumerable<BooleanResultBase> results) =>
        results
            .SelectMany(result =>
                result switch
                {
                    IBooleanOperationResult operationResult => GetAllAssertions(operationResult.Underlying),
                    _ => result.Explanation.AllAssertions
                });

    /// <remarks>
    /// Not verbatim: ticket #192 settled that descending <c>Explanation.Underlying</c> is the defect
    /// rather than the contract, so this descends the un-collapsed branches as the fold now does. The
    /// two formulations stay independent — <see cref="ExplanationBranches" /> rebuilds the children
    /// here rather than reading <c>Explanation.Branches</c> — but the behavioural claim is carried by
    /// <see cref="RootAssertionsBranchesTests" />, not by this comparison.
    /// </remarks>
    internal static IEnumerable<string> GetRootAssertions(BooleanResultBase result) =>
        RootAssertionsOf(result.Explanation).DistinctWithOrderPreserved();

    internal static IEnumerable<string> GetAllRootAssertions(BooleanResultBase result) =>
        GetAllRootAssertions(result.Underlying)
            .DistinctWithOrderPreserved()
            .ElseIfEmpty(result.Assertions);

    internal static IEnumerable<Explanation> ExplanationUnderlying(Explanation explanation) =>
        ResolveUnderlying(explanation.Assertions, explanation.Causes);

    internal static IEnumerable<Explanation> ExplanationAllUnderlying(Explanation explanation) =>
        ResolveAllUnderlying(explanation.Assertions, explanation.Results);

    /// <summary>
    /// An explanation's direct children, before the collapse <see cref="ResolveUnderlying" /> applies
    /// to them — the oracle's own formulation of <c>Explanation.Branches</c>.
    /// </summary>
    private static Explanation[] ExplanationBranches(IEnumerable<BooleanResultBase> causes) =>
        causes
            .SelectMany(cause =>
                cause switch
                {
                    IBooleanOperationResult => UnderlyingAssertionSources(cause),
                    _ => cause.ToEnumerable()
                })
            .Select(cause => cause.Explanation)
            .ToArray();

    private static IEnumerable<Explanation> ResolveUnderlying(
        IEnumerable<string> assertions,
        IEnumerable<BooleanResultBase> causes)
    {
        var underlying = ExplanationBranches(causes);

        var underlyingAssertions = underlying
            .SelectMany(explanation => explanation.Assertions)
            .DistinctWithOrderPreserved()
            .ToArray();

        var doesParentEqualChildAssertion = underlyingAssertions.SequenceEqual(assertions);

        return doesParentEqualChildAssertion
            ? underlying.SelectMany(ExplanationUnderlying).ToArray()
            : underlying;
    }

    private static IEnumerable<Explanation> ResolveAllUnderlying(
        IEnumerable<string> assertions,
        IEnumerable<BooleanResultBase> results)
    {
        var allUnderlying = results
            .SelectMany(result =>
                result switch
                {
                    IBooleanOperationResult => UnderlyingAllAssertionSources(result),
                    _ => result.ToEnumerable()
                })
            .Select(cause => cause.Explanation)
            .ToArray();

        var allUnderlyingAssertions = allUnderlying
            .SelectMany(explanation => explanation.AllAssertions)
            .DistinctWithOrderPreserved()
            .ToArray();

        var doesParentEqualChildAssertion = allUnderlyingAssertions.SequenceEqual(assertions);

        return doesParentEqualChildAssertion
            ? allUnderlying.SelectMany(ExplanationAllUnderlying).ToArray()
            : allUnderlying;
    }

    /// <summary>
    /// The deepest explanation of each branch, falling back to a branch's own assertions when that
    /// branch contributed nothing — per branch, which is the whole of ticket #192.
    /// </summary>
    private static string[] RootAssertionsOf(Explanation explanation)
    {
        var fromBranches = ExplanationBranches(explanation.Causes)
            .SelectMany(RootAssertionsOf)
            .ToArray();

        return fromBranches.Length == 0
            ? explanation.Assertions.ToArray()
            : fromBranches;
    }

    private static IEnumerable<string> GetAllRootAssertions(IEnumerable<BooleanResultBase> results) =>
        results.SelectMany(result => GetAllRootAssertions(result)
            .ElseIfEmpty(result.Assertions));
}
