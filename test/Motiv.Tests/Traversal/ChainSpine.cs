using Motiv.Traversal;

namespace Motiv.Tests.Traversal;

/// <summary>
/// The operation results down the left spine of a left-deep chain, shallowest first — the levels a
/// cost claim about such a chain is made over.
/// </summary>
internal static class ChainSpine
{
    internal static IEnumerable<BooleanResultBase<TMetadata>> Of<TMetadata>(
        BooleanResultBase<TMetadata> result)
    {
        for (var node = result; node is IBooleanOperationResult; node = node.UnderlyingWithValues.First())
            yield return node;
    }
}
