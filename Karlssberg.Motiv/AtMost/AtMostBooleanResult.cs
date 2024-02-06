using Humanizer;

namespace Karlssberg.Motiv.AtMost;

/// <summary>Represents the result of an "at most" boolean operation with a maximum number of satisfied operands.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
public sealed class AtMostBooleanResult<TMetadata> :
    BooleanResultBase<TMetadata>,
    ILogicalOperatorResult<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="AtMostBooleanResult{TMetadata}" /> class.</summary>
    /// <param name="isSatisfied"></param>
    /// <param name="maximum">The maximum number of satisfied operands allowed.</param>
    /// <param name="metadata">A function that creates metadata based on the overall satisfaction of the operands.</param>
    /// <param name="operandResults">The collection of operand results.</param>
    internal AtMostBooleanResult(
        bool isSatisfied,
        int maximum,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        Maximum = maximum.ThrowIfLessThan(0, nameof(maximum));
        
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        Value = isSatisfied;
    }

    /// <summary>Gets the maximum number of satisfied operands allowed.</summary>
    public int Maximum { get; }

    /// <summary>Gets the collection of operand results.</summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }

    /// <summary>
    ///     Gets the collection of determinative operand results that have the same satisfaction status as the overall
    ///     result.
    /// </summary>
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => Value switch
    {
        true => UnderlyingResults.Where(result => result.Value == Value),
        false => UnderlyingResults
    };

    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Value { get; }

    /// <summary>Gets the description of the boolean result.</summary>
    public override string Description => GetDescription();

    private string GetDescription()
    {
        var satisfiedCount = UnderlyingResults.Count(result => result.Value);
        var higherOrderStatement =
            $"AT_MOST_{Maximum}{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";
            
        return DeterminativeOperands.Any()
            ? $"{higherOrderStatement}({DeterminativeOperands.Count()}x {Reasons.Humanize()})"
            : higherOrderStatement;
    }

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands
        .SelectMany(r => r.GatherReasons());
}