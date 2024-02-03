using Humanizer;

namespace Karlssberg.Motiv.NSatisfied;

/// <summary>Represents a boolean result that is satisfied if n underlying results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel"></typeparam>
/// <typeparam name="TUnderlyingMetadata"></typeparam>
internal sealed class NSatisfiedBooleanResult<TModel, TMetadata>(
    int n,
    bool isSatisfied,
    IEnumerable<BooleanResultBase<TMetadata>> operandResults) :
    BooleanResultBase<TMetadata>,
    ILogicalOperatorResult<TMetadata>
{
    /// <summary>Gets the collection of operand results.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; } = operandResults
        .ThrowIfNull(nameof(operandResults))
        .ToArray();

    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => IsSatisfied
        ? UnderlyingResults.Where(result => result.IsSatisfied == IsSatisfied)
        : UnderlyingResults;

    /// <summary>
    /// Gets the collection of determinative operand results that have the same satisfaction status as the overall
    /// result.
    /// </summary>
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeResults => IsSatisfied switch
    {
        true => UnderlyingResults.Where(result => result.IsSatisfied),
        false => UnderlyingResults
    };

    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool IsSatisfied { get; } = isSatisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override string Description
    {
        get
        {
            var satisfiedCount = UnderlyingResults.Count(result => result.IsSatisfied);
            var higherOrderStatement =
                $"{n}_SATISFIED{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";

            return DeterminativeResults.Any()
                ? $"{higherOrderStatement}({SummarizeReasons()})"
                : higherOrderStatement;
        }
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

    /// <summary>Gets the reasons associated with the boolean result.</summary>
    public override IEnumerable<string> GatherReasons() => DeterminativeResults
        .SelectMany(r => r.GatherReasons());
}