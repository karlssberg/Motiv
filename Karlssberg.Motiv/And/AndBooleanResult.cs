namespace Karlssberg.Motiv.And;

/// <summary>
///     Represents the result of a boolean AND operation between two <see cref="BooleanResultBase{TMetadata}" />
///     objects.
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class AndBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> leftOperandResult,
    BooleanResultBase<TMetadata> rightOperandResult)
    : BooleanResultBase<TMetadata>
{
    /// <inheritdoc />
    public override bool Satisfied { get; } = leftOperandResult.Satisfied && rightOperandResult.Satisfied;

    /// <summary>Gets the result of the left operand.</summary>
    public BooleanResultBase<TMetadata> LeftOperandResult { get; } = leftOperandResult;

    /// <summary>Gets the result of the right operand.</summary>
    public BooleanResultBase<TMetadata> RightOperandResult { get; } = rightOperandResult;

    /// <summary>Gets an array containing the left and right operand results.</summary>
    public override IEnumerable<BooleanResultBase> UnderlyingResults => [LeftOperandResult, RightOperandResult];


    /// <inheritdoc />
    public override string Description => $"({LeftOperandResult}) AND:{IsSatisfiedDisplayText()} ({RightOperandResult})";

    /// <inheritdoc />
    public override Explanation Explanation => GetCausalResults().CreateExplanation();

    public override MetadataSet<TMetadata> Metadata => new(GetCausalResults()
        .SelectMany(result => result.Metadata));
    
    public override Cause<TMetadata> Cause => GetCausalResults().CreateCause();

    private IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults()
    {
        if (leftOperandResult.Satisfied == Satisfied)
            yield return leftOperandResult;
        if (rightOperandResult.Satisfied == Satisfied)
            yield return rightOperandResult;
    }
}