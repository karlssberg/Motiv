namespace Karlssberg.Motiv.OrElse;

internal sealed class OrElseBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata>? right = null)
    : BooleanResultBase<TMetadata>, IBinaryBooleanOperationResult<TMetadata>
{
    public override bool Satisfied { get; } = left.Satisfied || (right?.Satisfied ?? false);
    
    public override ResultDescriptionBase Description => 
        new OrElseBooleanResultDescription<TMetadata>(Operation, GetCauses());
    
    public override Explanation Explanation => GetCauses().CreateExplanation();

    public override MetadataNode<TMetadata> MetadataTier => CreateMetadataTier();
    
    public BooleanResultBase<TMetadata> Left { get; } = left;
    public BooleanResultBase<TMetadata>? Right { get; } = right;

    public string Operation => "OR ELSE";
    public bool IsCollapsable => true;
    
    BooleanResultBase IBinaryBooleanOperationResult.Left => Left;
    
    BooleanResultBase? IBinaryBooleanOperationResult.Right => Right;
    
    public override IEnumerable<BooleanResultBase> Underlying => GetUnderlying();

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => GetUnderlying();
    
    public override IEnumerable<BooleanResultBase> Causes => GetCauses();
    
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata => GetCauses();
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetCauses()
    {
        if (Satisfied == Left.Satisfied)
            yield return Left;
        
        if (Right is not null && Satisfied == Right.Satisfied)
            yield return Right;
    }
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetUnderlying()
    {
        yield return Left;
        
        if (Right is not null)
            yield return Right;
    }
    
    private MetadataNode<TMetadata> CreateMetadataTier() =>
        new(CausesWithMetadata.GetMetadata(), CausesWithMetadata);
}