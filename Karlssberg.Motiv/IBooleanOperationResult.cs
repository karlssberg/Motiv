namespace Karlssberg.Motiv;

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
    IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata { get; }
    
    IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata { get; }
    MetadataTree<TMetadata> MetadataTree { get; }
}