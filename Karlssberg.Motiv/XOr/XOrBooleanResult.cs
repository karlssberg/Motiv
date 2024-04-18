namespace Karlssberg.Motiv.XOr;

/// <summary>Represents the result of a logical XOR (exclusive OR) operation.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the result.</typeparam>
internal sealed class XOrBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : BooleanResultBase<TMetadata>, IBinaryBooleanOperationResult<TMetadata>
{
    /// <summary>Gets a value indicating whether the XOR operation is satisfied.</summary>
    public override bool Satisfied { get; } = left.Satisfied ^ right.Satisfied;

    public override Explanation Explanation => GetResults().CreateExplanation();

    /// <summary>Gets the description of the XOR operation.</summary>
    public override ResultDescriptionBase Description =>
        new XOrBooleanResultDescription<TMetadata>(Left, Right, GetResults());
    
    public BooleanResultBase<TMetadata> Left { get; } = left;

    public BooleanResultBase<TMetadata> Right { get; } = right;

    BooleanResultBase IBinaryBooleanOperationResult.Left => Left;
    
    BooleanResultBase? IBinaryBooleanOperationResult.Right => Right;

    public override MetadataTree<TMetadata> MetadataTree => CreateMetadataTree();
    public override IEnumerable<BooleanResultBase> Underlying => GetResults();
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => GetResults();
    public override IEnumerable<BooleanResultBase> Causes => GetResults();
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata => GetResults();
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetResults() => 
        Left.ToEnumerable()
            .Append(Right);

    private MetadataTree<TMetadata> CreateMetadataTree()
    {
        var causes = GetResults().ToArray();
        var underlying =  causes
            .SelectMany(cause => cause.MetadataTree.Underlying);
        
        return new MetadataTree<TMetadata>(causes.GetMetadata(), underlying);
    }
}