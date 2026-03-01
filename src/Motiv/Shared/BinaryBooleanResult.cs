using Motiv.Traversal;

namespace Motiv.Shared;

/// <summary>
///     Base class for binary boolean operation results (AND, OR, XOR, AND ALSO, OR ELSE).
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal abstract class BinaryBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata>? right)
    : BooleanResultBase<TMetadata>, IBinaryBooleanOperationResult<TMetadata>
{
    public BooleanResultBase<TMetadata> Left { get; } = left;

    public BooleanResultBase<TMetadata>? Right { get; } = right;

    BooleanResultBase IBinaryBooleanOperationResult.Left => Left;

    BooleanResultBase? IBinaryBooleanOperationResult.Right => Right;

    public abstract string Operation { get; }

    public abstract bool IsCollapsable { get; }

    private Explanation? _explanation;
    public override Explanation Explanation => _explanation ??= new(GetCausalResults(), Underlying);

    private MetadataNode<TMetadata>? _metadataTier;
    public override MetadataNode<TMetadata> MetadataTier =>
        _metadataTier ??= new(CausesWithValues.GetValues(), CausesWithValues);

    public override IEnumerable<BooleanResultBase> Underlying => GetAllResults();

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues => GetAllResults();

    public override IEnumerable<BooleanResultBase> Causes => GetCausalResults();

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => GetCausalResults();

    protected virtual IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults()
    {
        if (Left.Satisfied == Satisfied)
            yield return Left;

        if (Right is not null && Right.Satisfied == Satisfied)
            yield return Right;
    }

    protected IEnumerable<BooleanResultBase<TMetadata>> GetAllResults()
    {
        yield return Left;

        if (Right is not null)
            yield return Right;
    }
}
