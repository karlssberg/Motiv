using Motiv.Shared;

namespace Motiv.Traversal;

internal interface IBooleanOperationResult
{
    ResultDescriptionBase Description { get; }

    IEnumerable<BooleanResultBase> Underlying { get; }

    IEnumerable<BooleanResultBase> Causes { get; }

    IEnumerable<string> Assertions { get; }
    Explanation Explanation { get; }
    IEnumerable<BooleanResultBase> UnderlyingAssertionSources  { get; }
}

internal interface IBooleanOperationResult<TMetadata> : IBooleanOperationResult
{
    IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues { get; }

    IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues { get; }
    MetadataNode<TMetadata> MetadataTier { get; }
}
