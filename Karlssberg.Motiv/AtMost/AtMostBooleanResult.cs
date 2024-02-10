using Humanizer;

namespace Karlssberg.Motiv.AtMost;

/// <summary>Represents the result of an "at most" boolean operation with a maximum number of satisfied operands.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
public sealed class AtMostBooleanResult<TMetadata>(
    bool isSatisfied,
    int maximum,
    IReadOnlyCollection<BooleanResultBase<TMetadata>> operandResults)
    : BooleanResultBase<TMetadata>, ILogicalOperatorResult<TMetadata>
{
    /// <summary>Gets the maximum number of satisfied operands allowed.</summary>
    public int Maximum { get; } = maximum.ThrowIfLessThan(0, nameof(maximum));

    /// <summary>Gets the collection of operand results.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults => operandResults;

    /// <summary>
    /// Gets the collection of determinative operand results that have the same satisfaction status as the overall
    /// result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands =>
        UnderlyingResults.Where(result => result.Satisfied);

    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Satisfied => isSatisfied;
    
    /// <summary>Gets the description of the boolean result.</summary>
    public override string Description => GetDescription();

    private string GetDescription()
    {
        var satisfiedCount = UnderlyingResults.Count(result => result.Satisfied);
        var higherOrderStatement =
            $"AT_MOST_{Maximum}{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";

        return DeterminativeOperands.Any()
            ? $"{higherOrderStatement}({ReasonHierarchy.SummarizeReasons()})"
            : higherOrderStatement;
    }

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override IEnumerable<Reason> ReasonHierarchy => DeterminativeOperands
        .SelectMany(result => result.ReasonHierarchy);
}