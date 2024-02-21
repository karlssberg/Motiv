using Humanizer;

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

    /// <inheritdoc />
    public override string Description => GetCausalResults().Select(result => result.Description)
        .Humanize();

    internal override string DebuggerDisplay() =>
        $"({leftOperandResult}) AND:{IsSatisfiedDisplayText()} ({rightOperandResult})";

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