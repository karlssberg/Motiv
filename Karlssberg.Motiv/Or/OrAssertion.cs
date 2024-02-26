namespace Karlssberg.Motiv.Or;

/// <summary>Represents a boolean result that is the logical OR of two operand results.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class OrAssertion<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : BooleanResultBase<TMetadata>, ICompositeAssertion
{
    public override Reason Reason => GetCausalResults().CreateReason();
    
    public override MetadataSet<TMetadata> Metadata => new(GetCausalResults()
        .SelectMany(result => result.Metadata));
    
    public override CausalMetadata<TMetadata> CausalMetadata => GetCausalResults().CreateCause();

    /// <inheritdoc />`
    public override bool Satisfied { get; } = left.Satisfied || right.Satisfied;

    /// <inheritdoc />
    public override IAssertion Assertion => new OrBooleanAssertion<TMetadata>(left, right, GetCausalResults());
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults()
    {
        if (left.Satisfied == Satisfied)
            yield return left;
        
        if (right.Satisfied == Satisfied)
            yield return right;
    }
}