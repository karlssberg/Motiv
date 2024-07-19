namespace Motiv.Or;

/// <summary>Represents a boolean result that is the logical OR of two operand results.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class OrBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : BooleanResultBase<TMetadata>, IBinaryBooleanOperationResult<TMetadata>
{
    public override Explanation Explanation => GetCausalResults().CreateExplanation();
    
    public override MetadataNode<TMetadata> MetadataTier => CreateMetadataTier();
    
    public override IEnumerable<BooleanResultBase> Underlying => GetResults();
    
    public BooleanResultBase<TMetadata> Left { get; } = left;

    public BooleanResultBase<TMetadata> Right { get; } = right;

    BooleanResultBase IBinaryBooleanOperationResult.Left => Left;
    
    BooleanResultBase IBinaryBooleanOperationResult.Right => Right;

    public string Operation => "OR";
    public bool IsCollapsable => true;
    
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues => GetResults();
    
    public override IEnumerable<BooleanResultBase> Causes => GetCausalResults();
    
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => GetCausalResults();

    public override bool Satisfied { get; } = left.Satisfied || right.Satisfied;

    public override ResultDescriptionBase Description => new OrBooleanResultDescription<TMetadata>(Operation, GetCausalResults());

    private IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults()
    {
        if (Left.Satisfied == Satisfied)
            yield return Left;
        
        if (Right.Satisfied == Satisfied)
            yield return Right;
    }
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetResults()
    {
        yield return Left;
        yield return Right;
    }
    
    private MetadataNode<TMetadata> CreateMetadataTier() =>
        new(CausesWithValues.GetValues(), CausesWithValues);
}