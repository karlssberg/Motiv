namespace Karlssberg.Motiv.Or;

/// <summary>Represents a boolean result that is the logical OR of two operand results.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class OrBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> leftOperandResult,
    BooleanResultBase<TMetadata> rightOperandResult)
    : BooleanResultBase<TMetadata>
{

    public override Explanation Explanation => GetCausalResults().CreateExplanation();
    
    public override MetadataSet<TMetadata> Metadata => new(GetCausalResults()
        .SelectMany(result => result.Metadata));
    
    public override Cause<TMetadata> Cause => GetCausalResults().CreateCause();

    /// <inheritdoc />`
    public override bool Satisfied { get; } = leftOperandResult.Satisfied || rightOperandResult.Satisfied;

    /// <inheritdoc />
    public override string Description => $"({leftOperandResult}) OR:{IsSatisfiedDisplayText()} ({rightOperandResult})";
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults()
    {
        if (leftOperandResult.Satisfied == Satisfied)
            yield return leftOperandResult;
        if (rightOperandResult.Satisfied == Satisfied)
            yield return rightOperandResult;
    }
}