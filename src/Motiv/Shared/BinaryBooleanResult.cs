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

    protected BooleanResultBase<TMetadata>[] CausalResults => field ??= GetCausalResults();

    private protected BooleanResultBase<TMetadata>[] AllResults =>
        field ??= Right is null ? [Left] : [Left, Right];

    public override Explanation Explanation => field ??= new(CausalResults, AllResults);

    public override MetadataNode<TMetadata> MetadataTier =>
        field ??= new(CausalResults.GetValues(), CausalResults);

    public override IEnumerable<BooleanResultBase> Underlying => AllResults;

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues => AllResults;

    public override IEnumerable<BooleanResultBase> Causes => CausalResults;

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => CausalResults;

    protected virtual BooleanResultBase<TMetadata>[] GetCausalResults() =>
        (Left.Satisfied == Satisfied, Right is not null && Right.Satisfied == Satisfied) switch
        {
            (true, true) => [Left, Right!],
            (true, _) => [Left],
            (_, true) => [Right!],
            _ => []
        };
}
