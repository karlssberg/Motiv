namespace Motiv.OrElse;

internal sealed class OrElseBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata>? right = null)
    : BooleanResultBase<TMetadata>, IBinaryBooleanOperationResult<TMetadata>
{
    public override bool Satisfied { get; } = left.Satisfied || (right?.Satisfied ?? false);

    public override ResultDescriptionBase Description =>
        new OrElseBooleanResultDescription<TMetadata>(Operation, GetCauses());

    public override Explanation Explanation => new(GetCauses(), Underlying);

    public override MetadataNode<TMetadata> MetadataTier => CreateMetadataTier();

    public BooleanResultBase<TMetadata> Left { get; } = left;

    public BooleanResultBase<TMetadata>? Right { get; } = right;

    public string Operation => Operator.Or;

    public bool IsCollapsable => true;

    BooleanResultBase IBinaryBooleanOperationResult.Left => Left;

    BooleanResultBase? IBinaryBooleanOperationResult.Right => Right;

    public override IEnumerable<BooleanResultBase> Underlying => GetUnderlying();

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues => GetUnderlying();

    public override IEnumerable<BooleanResultBase> Causes => GetCauses();

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => GetCauses();

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
        new(CausesWithValues.GetValues(), CausesWithValues);
}
