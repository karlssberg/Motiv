namespace Karlssberg.Motiv.XOr;

/// <summary>Represents the result of a logical XOR (exclusive OR) operation.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the result.</typeparam>
internal sealed class XOrBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : BooleanResultBase<TMetadata>, ICompositeBooleanResult
{
    /// <summary>Gets a value indicating whether the XOR operation is satisfied.</summary>
    public override bool Satisfied => left.Satisfied ^ right.Satisfied;

    public override Explanation Explanation => GetCausalResults().CreateExplanation();

    /// <summary>Gets the description of the XOR operation.</summary>
    public override ResultDescriptionBase Description =>
        new XOrBooleanResultDescription<TMetadata>(left, right, GetCausalResults());

    public override MetadataSet<TMetadata> Metadata => CreateMetadataSet();
    
    private MetadataSet<TMetadata> CreateMetadataSet()
    {
        var metadataSets = GetCausalResults().Select(result => result.Metadata).ToArray();
        return new(
            metadataSets.SelectMany(metadataSet => metadataSet),
            metadataSets.SelectMany(metadataSet => metadataSet.Underlying));
    }
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults() => 
        left.ToEnumerable()
            .Append(right);
}