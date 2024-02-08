using Humanizer;

namespace Karlssberg.Motiv.AtLeast;

/// <summary>Represents a boolean result that is satisfied if at least a specified number of operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
public sealed class AtLeastBooleanResult<TMetadata>(
    bool isSatisfied,
    int minimum,
    IReadOnlyCollection<BooleanResultBase<TMetadata>> operandResults) 
    : BooleanResultBase<TMetadata>, ILogicalOperatorResult<TMetadata>
{
    /// <summary>Gets the minimum number of operand results that need to be satisfied.</summary>
    public int Minimum { get; } = minimum.ThrowIfLessThan(0, nameof(minimum));

    /// <summary>Gets the collection of operand results.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; } =
        operandResults.ThrowIfNull(nameof(operandResults));

    /// <summary>
    /// Gets the collection of determinative operand results that have the same satisfaction status as the overall
    /// result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands =>
        UnderlyingResults.Where(result => result.Value);

    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Value => isSatisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override string Description => GetDescription();

    private string GetDescription()
    {
        var satisfiedCount = UnderlyingResults.Count(result => result.Value);
        var higherOrderStatement =
            $"AT_LEAST_{Minimum}{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";

        return DeterminativeOperands.Any()
            ? $"{higherOrderStatement}({DeterminativeOperands.Count()}x {Reasons.Humanize()})"
            : higherOrderStatement;
    }

    /// <summary>Gets the reasons associated with the boolean result.</summary>
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands
        .SelectMany(r => r.GatherReasons());
}