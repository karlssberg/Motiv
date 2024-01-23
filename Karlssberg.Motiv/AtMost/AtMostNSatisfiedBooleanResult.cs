using Humanizer;

namespace Karlssberg.Motiv.AtMost;

/// <summary>Represents the result of an "at most" boolean operation with a maximum number of satisfied operands.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
public sealed class AtMostNSatisfiedBooleanResult<TMetadata> : BooleanResultBase<TMetadata>, IAtMostNSatisfiedBooleanResult<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="AtMostNSatisfiedBooleanResult{TMetadata}" /> class.</summary>
    /// <param name="isSatisfied"></param>
    /// <param name="maximum">The maximum number of satisfied operands allowed.</param>
    /// <param name="metadata">A function that creates metadata based on the overall satisfaction of the operands.</param>
    /// <param name="operandResults">The collection of operand results.</param>
    internal AtMostNSatisfiedBooleanResult(
        bool isSatisfied,
        int maximum,
        IEnumerable<TMetadata> metadata,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        Maximum = maximum.ThrowIfLessThan(0, nameof(maximum));
        
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadata;
    }

    /// <summary>Gets the maximum number of satisfied operands allowed.</summary>
    public int Maximum { get; }

    /// <summary>Gets the substitute metadata associated with the boolean result.</summary>
    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    /// <summary>Gets the collection of operand results.</summary>
    public IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }

    /// <summary>
    ///     Gets the collection of determinative operand results that have the same satisfaction status as the overall
    ///     result.
    /// </summary>
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeResults => 
        UnderlyingResults.Where(result => result.IsSatisfied);

    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool IsSatisfied { get; }

    /// <summary>Gets the description of the boolean result.</summary>
    public override string Description
    {
        
        get
        {
            var satisfiedCount = UnderlyingResults.Count(result => result.IsSatisfied);
            var higherOrderStatement =
                $"AT_MOST_{Maximum}{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";
            
            return DeterminativeResults.Any()
                ? $"{higherOrderStatement}({DeterminativeResults.Count()}x {Reasons.Humanize()})"
                : higherOrderStatement;
        }
    }

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override IEnumerable<string> GatherReasons() => DeterminativeResults.SelectMany(r => r.GatherReasons());
}