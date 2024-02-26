namespace Karlssberg.Motiv.And;

/// <summary>
///     Represents the result of a boolean AND operation between two <see cref="BooleanResultBase{TMetadata}" />
///     objects.
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class AndAssertion<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : BooleanResultBase<TMetadata>, ICompositeAssertion
{
    /// <inheritdoc />
    public override bool Satisfied { get; } = left.Satisfied && right.Satisfied;

    /// <inheritdoc />
    public override IAssertion Assertion =>
        new AndBooleanAssertion<TMetadata>(left, right, GetCausalResults());

    /// <inheritdoc />
    public override Reason Reason => GetCausalResults().CreateReason();

    public override MetadataSet<TMetadata> Metadata => new(GetCausalResults()
        .SelectMany(result => result.Metadata));
    
    public override CausalMetadata<TMetadata> CausalMetadata => GetCausalResults().CreateCause();

    private IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults()
    {
        if (left.Satisfied == Satisfied)
            yield return left;
        if (right.Satisfied == Satisfied)
            yield return right;
    }
}