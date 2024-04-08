namespace Karlssberg.Motiv.Or;

/// <summary>Represents a boolean result that is the logical OR of two operand results.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class OrBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : BooleanResultBase<TMetadata>, IBinaryOperationBooleanResult
{
    public override Explanation Explanation => GetCausalResults().CreateExplanation();
    
    public override MetadataTree<TMetadata> MetadataTree => CreateMetadataSet();
    
    public override IEnumerable<BooleanResultBase> Underlying => GetResults();
    
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => GetResults();
    
    public override IEnumerable<BooleanResultBase> Causes => GetCausalResults();
    
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata => GetCausalResults();

    /// <inheritdoc />`
    public override bool Satisfied { get; } = left.Satisfied || right.Satisfied;

    public override ResultDescriptionBase Description => new OrBooleanResultDescription<TMetadata>(left, right, GetCausalResults());

    private MetadataTree<TMetadata> CreateMetadataSet()
    {
        var metadataSets = GetCausalResults().Select(result => result.MetadataTree).ToArray();
        return new(
            metadataSets.SelectMany(metadataSet => metadataSet),
            metadataSets.SelectMany(metadataSet => metadataSet.Underlying));
    }

    private IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults()
    {
        if (left.Satisfied == Satisfied)
            yield return left;
        
        if (right.Satisfied == Satisfied)
            yield return right;
    }
    private IEnumerable<BooleanResultBase<TMetadata>> GetResults()
    {
            yield return left;
            yield return right;
    }
}