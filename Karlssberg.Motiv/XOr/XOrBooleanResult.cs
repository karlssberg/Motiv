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

    public override Explanation Explanation => GetResults().CreateExplanation();

    /// <summary>Gets the description of the XOR operation.</summary>
    public override ResultDescriptionBase Description =>
        new XOrBooleanResultDescription<TMetadata>(left, right, GetResults());

    public override MetadataSet<TMetadata> Metadata => CreateMetadataSet();
    public override IEnumerable<BooleanResultBase> Underlying => GetResults();
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => GetResults();
    public override IEnumerable<BooleanResultBase> Causes => GetResults();
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata => GetResults();

    private MetadataSet<TMetadata> CreateMetadataSet()
    {
        var metadataSets = GetResults().Select(result => result.Metadata).ToArray();
        return new(
            metadataSets.SelectMany(metadataSet => metadataSet),
            metadataSets.SelectMany(metadataSet => metadataSet.Underlying));
    }
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetResults() => 
        left.ToEnumerable()
            .Append(right);
}