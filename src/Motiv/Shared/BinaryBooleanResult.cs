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

    private BooleanResultBase<TMetadata>[]? _causalResults;
    protected BooleanResultBase<TMetadata>[] CausalResults => _causalResults ??= GetCausalResults();

    private BooleanResultBase<TMetadata>[]? _allResults;
    private protected BooleanResultBase<TMetadata>[] AllResults =>
        _allResults ??= Right is null ? [Left] : [Left, Right];

    private Explanation? _explanation;
    public override Explanation Explanation => _explanation ??= new(CausalResults, AllResults);

    private MetadataNode<TMetadata>? _metadataTier;
    public override MetadataNode<TMetadata> MetadataTier =>
        _metadataTier ??= new(CausalResults.GetValues(), CausalResults);

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
