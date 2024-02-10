using Humanizer;

namespace Karlssberg.Motiv.Any;

/// <summary>Represents a boolean result that indicates whether any of the operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel">The type of the model used to evaluate each underlying operand. </typeparam>
internal sealed class AnyBooleanResult<TModel, TMetadata>(
    IReadOnlyCollection<BooleanResultBase<TMetadata>> operandResults)
    : BooleanResultBase<TMetadata>, ILogicalOperatorResult<TMetadata>
{
    /// <inheritdoc />
    public override bool Satisfied { get; } = operandResults.Any(result => result.Satisfied);
    
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults => operandResults;

    /// <summary>Gets the determinative operand results that have the same satisfaction status as the boolean result.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => UnderlyingResults
        .Where(result => result.Satisfied == Satisfied);

    /// <inheritdoc />
    public override string Description => GetDescription();
    
    private string GetDescription()
    {
        var satisfiedCount = UnderlyingResults.Count(result => result.Satisfied);
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
    public override IEnumerable<Reason> GatherReasons() => DeterminativeOperands
        .SelectMany(r => r.GatherReasons());
}