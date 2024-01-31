using Humanizer;

namespace Karlssberg.Motiv.Any;

/// <summary>Represents a boolean result that indicates whether any of the operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel">The type of the model used to evaluate each underlying operand. </typeparam>
public sealed class AnySatisfiedBooleanResult<TModel, TMetadata> :
    BooleanResultBase<TMetadata>,
    ILogicalOperatorResult<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="AnySatisfiedBooleanResult{TMetadata}" /> class.</summary>
    /// <param name="isSatisfied"></param>
    /// <param name="operandResults">The operand results to evaluate.</param>
    internal AnySatisfiedBooleanResult(
        bool isSatisfied,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        IsSatisfied = isSatisfied;
    }

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }

    /// <summary>Gets the determinative operand results that have the same satisfaction status as the boolean result.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => UnderlyingResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    /// <inheritdoc />
    public override bool IsSatisfied { get; }

    /// <inheritdoc />
    public override string Description =>
        $"ANY{{{DeterminativeOperands.Count()}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}({DeterminativeOperands.Distinct().Humanize()})";

    /// <inheritdoc />
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands.SelectMany(r => r.GatherReasons());
}