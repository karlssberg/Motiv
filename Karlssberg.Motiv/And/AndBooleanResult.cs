namespace Karlssberg.Motiv.And;

/// <summary>
///     Represents the result of a boolean AND operation between two <see cref="BooleanResultBase{TMetadata}" />
///     objects.
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class AndBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : BooleanResultBase<TMetadata>, ICompositeBooleanResult
{
    /// <inheritdoc />
    public override bool Satisfied { get; } = left.Satisfied && right.Satisfied;

    /// <inheritdoc />
    public override ResultDescriptionBase Description =>
        new AndBooleanResultDescription<TMetadata>(left, right, GetCausalResults());

    /// <inheritdoc />
    public override Explanation Explanation => GetCausalResults().CreateReason();

    public override MetadataSet<TMetadata> Metadata => new(GetCausalResults()
        .SelectMany(result => result.Metadata));
    
    public override CausalMetadataCollection<TMetadata> CausalMetadata => GetCausalResults().CreateCause();

    private IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults()
    {
        if (left.Satisfied == Satisfied)
            yield return left;
        if (right.Satisfied == Satisfied)
            yield return right;
    }
}