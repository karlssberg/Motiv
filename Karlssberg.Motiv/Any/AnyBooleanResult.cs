using Humanizer;

namespace Karlssberg.Motiv.Any;

/// <summary>Represents a boolean result that indicates whether any of the operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel">The type of the model used to evaluate each underlying operand. </typeparam>
internal sealed class AnyBooleanResult<TModel, TMetadata>(
    IReadOnlyCollection<BooleanResultBase<TMetadata>> operandResults)
    : BooleanResultBase<TMetadata>, ILogicalOperatorResult<TMetadata>
{
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; } =
        operandResults.ThrowIfNull(nameof(operandResults));

    /// <summary>Gets the determinative operand results that have the same satisfaction status as the boolean result.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => UnderlyingResults
        .Where(result => result.Value == Value);

    /// <inheritdoc />
    public override bool Value { get; } = operandResults.Any(result => result.Value);

    /// <inheritdoc />
    public override string Description => GetDescription();
    
    private string GetDescription()
    {
        var satisfiedCount = UnderlyingResults.Count(result => result.Value);
        var higherOrderStatement =
            $"ANY{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";

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
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands.SelectMany(r => r.GatherReasons());
}