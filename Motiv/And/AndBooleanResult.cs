namespace Motiv.And;

/// <summary>
///     Represents the result of a boolean AND operation between two <see cref="BooleanResultBase{TMetadata}" />
///     objects.
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class AndBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : BooleanResultBase<TMetadata>, IBinaryBooleanOperationResult<TMetadata>
{
    public override bool Satisfied { get; } = left.Satisfied && right.Satisfied;
    
    public override ResultDescriptionBase Description =>
        new AndBooleanResultDescription<TMetadata>(Operation, GetCausalResults());

    public override Explanation Explanation => GetCausalResults().CreateExplanation();

    public override MetadataNode<TMetadata> MetadataTier => CreateMetadataTier();
    
    public override IEnumerable<BooleanResultBase> Underlying => GetResults();
    
    public BooleanResultBase<TMetadata> Left { get; } = left;

    public BooleanResultBase<TMetadata> Right { get; } = right;

    BooleanResultBase IBinaryBooleanOperationResult.Left => Left;
    
    BooleanResultBase IBinaryBooleanOperationResult.Right => Right;

    public string Operation => "AND";
    public bool IsCollapsable => true;

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => GetResults();

    public override IEnumerable<BooleanResultBase> Causes => GetCausalResults();

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata => GetCausalResults();

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
        new(CausesWithMetadata.GetMetadata(), CausesWithMetadata);
}