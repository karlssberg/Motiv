namespace Karlssberg.Motiv.OrElse;

internal sealed class OrElseBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata>? right = null)
    : BooleanResultBase<TMetadata>
{
    public override bool Satisfied { get; } = left.Satisfied || (right?.Satisfied ?? false);
    
    public override ResultDescriptionBase Description => 
        new OrElseBooleanResultDescription<TMetadata>(left, right, GetCauses());
    
    public override Explanation Explanation => GetCauses().CreateExplanation();

    public override MetadataTree<TMetadata> MetadataTree => CreateMetadataTree();

    public override IEnumerable<BooleanResultBase> Underlying => GetUnderlying();

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => GetUnderlying();
    
    public override IEnumerable<BooleanResultBase> Causes => GetCauses();
    
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata => GetCauses();
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetCauses()
    {
        if (Satisfied == left.Satisfied)
            yield return left;
        
        if (right is not null && Satisfied == right.Satisfied)
            yield return right;
    }
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetUnderlying()
    {
        yield return left;
        
        if (right is not null)
            yield return right;
    }
    
    private MetadataTree<TMetadata> CreateMetadataTree()
    {
        var causes = GetCauses().ToArray();
        var underlying =  causes
            .SelectMany(cause => cause.MetadataTree.Underlying);
        
        return new MetadataTree<TMetadata>(causes.GetMetadata(), underlying);
    }
}