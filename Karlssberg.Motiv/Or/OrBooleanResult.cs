namespace Karlssberg.Motiv.Or;

/// <summary>Represents a boolean result that is the logical OR of two operand results.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class OrBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> leftOperandResult,
    BooleanResultBase<TMetadata> rightOperandResult)
    : BooleanResultBase<TMetadata>
{
    /// <summary>Gets the result of the left operand.</summary>
    public BooleanResultBase<TMetadata> LeftOperandResult => leftOperandResult;

    /// <summary>Gets the result of the right operand.</summary>
    public BooleanResultBase<TMetadata> RightOperandResult => rightOperandResult;

    /// <summary>Gets an array containing the left and right operand results.</summary>
    public override IEnumerable<BooleanResultBase> UnderlyingResults => [LeftOperandResult, RightOperandResult];

    public override Explanation Explanation => GetCausalResults().CreateExplanation();
    
    public override MetadataSet<TMetadata> Metadata => new(GetCausalResults()
        .SelectMany(result => result.Metadata));
    
    public override Cause<TMetadata> Cause => GetCausalResults().CreateCause();

    /// <inheritdoc />`
    public override bool Satisfied { get; } = leftOperandResult.Satisfied || rightOperandResult.Satisfied;

    /// <inheritdoc />
    public override string Description => $"({LeftOperandResult}) OR:{IsSatisfiedDisplayText()} ({RightOperandResult})";
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults()
    {
        if (LeftOperandResult.Satisfied == Satisfied)
            yield return leftOperandResult;
        if (RightOperandResult.Satisfied == Satisfied)
            yield return rightOperandResult;
    }
}