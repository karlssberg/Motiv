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
    public override ExplanationTree ExplanationTree => GetCausalResults().CreateExplanation();

    /// <inheritdoc />
    public override MetadataTree<TMetadata> MetadataTree => CreateMetadataSet();
    
    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase> Underlying => GetResults();

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => GetResults();

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase> Causes => GetCausalResults();

    /// <inheritdoc />
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata => GetCausalResults();

    private MetadataTree<TMetadata> CreateMetadataSet()
    {
        var metadataSets = GetCausalResults().Select(result => result.MetadataTree).ToArray();
        return new(metadataSets.SelectMany(metadataSet => metadataSet),
            metadataSets.SelectMany(metadataSet => metadataSet.Underlying));
    }

    private IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults()
    {
        if (left.Satisfied == Satisfied)
            yield return left;
        if (right.Satisfied == Satisfied)
            yield return right;
    }
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetResults()
    {
            yield return left;
            yield return right;
    }
}