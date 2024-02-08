using Humanizer;

namespace Karlssberg.Motiv.All;

/// <summary>Represents a boolean result that indicates whether all operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel">The model used to evaluate each underlying operand.</typeparam>
/// <typeparam name="TMetadata">The type of metadata associated with each underlying operand.</typeparam>
internal sealed class AllBooleanResult<TMetadata>(
    bool isSatisfied,
    IReadOnlyCollection<BooleanResultBase<TMetadata>> operandResults)
    : BooleanResultBase<TMetadata>, ILogicalOperatorResult<TMetadata>, IHigherOrderLogicalOperatorResult<TMetadata>
{
    /// <summary>Gets the collection of all the operand results.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; } =
        operandResults.ThrowIfNull(nameof(operandResults));

    /// <summary>Gets the determinative operand results that have the same satisfaction as the overall result.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => UnderlyingResults
        .Where(result => result.Value == Value);

    /// <inheritdoc cref="BooleanResultBase{TMetadata}.Value" />
    public override bool Value => isSatisfied;

    /// <inheritdoc cref="BooleanResultBase{TMetadata}.Description" />
    public override string Description => GetDescription();

    private string GetDescription()
    {
        var satisfiedCount = UnderlyingResults.Count(result => result.Value);
        var higherOrderStatement =
            $"ALL{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";

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

    /// <inheritdoc />
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands
        .SelectMany(r => r.GatherReasons());
}