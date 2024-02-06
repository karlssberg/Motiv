using Humanizer;

namespace Karlssberg.Motiv.Any;

/// <summary>Represents a boolean result that indicates whether any of the operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel">The type of the model used to evaluate each underlying operand. </typeparam>
internal sealed class AnyBooleanResult<TModel, TMetadata>(
    bool isSatisfied,
    IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    : BooleanResultBase<TMetadata>,
    ILogicalOperatorResult<TMetadata>
{
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; } = operandResults
        .ThrowIfNull(nameof(operandResults))
        .ToArray();

    /// <summary>Gets the determinative operand results that have the same satisfaction status as the boolean result.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => UnderlyingResults
        .Where(result => result.Value == Value);

    /// <inheritdoc />
    public override bool Value => isSatisfied;

    /// <inheritdoc />
    public override string Description =>
        $"ANY{{{DeterminativeOperands.Count()}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}({DeterminativeOperands.Distinct().Humanize()})";

    /// <inheritdoc />
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands.SelectMany(r => r.GatherReasons());
}