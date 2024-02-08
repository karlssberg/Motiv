using Humanizer;

namespace Karlssberg.Motiv.Exactly;

/// <summary>Represents a boolean result that is satisfied if exactly <c>n</c> underlying results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel"></typeparam>
/// <typeparam name="TUnderlyingMetadata"></typeparam>
internal sealed class ExactlyBooleanResult<TModel, TMetadata>(
    int n,
    bool isSatisfied,
    IReadOnlyCollection<BooleanResultBase<TMetadata>> operandResults)
    : BooleanResultBase<TMetadata>, ILogicalOperatorResult<TMetadata>
{
    /// <summary>Gets the collection of operand results.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; } =
        operandResults.ThrowIfNull(nameof(operandResults));

    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => Value switch
    {
        true => UnderlyingResults.Where(result => result.Value),
        false => UnderlyingResults
    };

    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Value { get; } = isSatisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override string Description => GetDescription();

    private string GetDescription()
    {
        var satisfiedCount = UnderlyingResults.Count(result => result.Value);
        var higherOrderStatement =
            $"{n}_SATISFIED{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";

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

    /// <summary>Gets the reasons associated with the boolean result.</summary>
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands
        .SelectMany(r => r.GatherReasons());
}