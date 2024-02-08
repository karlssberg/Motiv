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
            $"AT_MOST_{Maximum}{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";

        return DeterminativeOperands.Any()
            ? $"{higherOrderStatement}({SummarizeReasons()})"
            : higherOrderStatement;
    }

    private string SummarizeReasons()
    {
        return GatherReasons()
            .GroupBy(reason => reason)
            .Select(grouping => grouping.Count() == 1
                ? $"{grouping.Key}"
                : $"{grouping.Key} x{grouping.Count()}")
            .Humanize();
    }

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands
        .SelectMany(r => r.GatherReasons());
}