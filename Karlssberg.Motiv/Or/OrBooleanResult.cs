namespace Karlssberg.Motiv.Or;

/// <summary>Represents a boolean result that is the logical OR of two operand results.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class OrBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : BooleanResultBase<TMetadata>, ICompositeBooleanResult
{
    public override Explanation Explanation => GetCausalResults().CreateReason();
    
    public override MetadataSet<TMetadata> Metadata => CreateMetadataSet();

    private MetadataSet<TMetadata> CreateMetadataSet()
    {
        var metadataSets = GetCausalResults().Select(result => result.Metadata).ToArray();
        return new(
            metadataSets.SelectMany(metadataSet => metadataSet),
            metadataSets.SelectMany(metadataSet => metadataSet.Underlying));
    }

    /// <inheritdoc />`
    public override bool Satisfied { get; } = left.Satisfied || right.Satisfied;

    /// <inheritdoc />
    public override ResultDescriptionBase Description => new OrBooleanResultDescription<TMetadata>(left, right, GetCausalResults());
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults()
    {
        if (left.Satisfied == Satisfied)
            yield return left;
        
        if (right.Satisfied == Satisfied)
            yield return right;
    }
}