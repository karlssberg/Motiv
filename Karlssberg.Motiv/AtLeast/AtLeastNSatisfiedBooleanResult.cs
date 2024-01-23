using Humanizer;

namespace Karlssberg.Motiv.AtLeast;

/// <summary>Represents a boolean result that is satisfied if at least a specified number of operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
public sealed class AtLeastNSatisfiedBooleanResult<TMetadata> : BooleanResultBase<TMetadata>, IAtLeastNSatisfiedBooleanResult<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="AtLeastNSatisfiedBooleanResult{TMetadata}" /> class.</summary>
    /// <param name="isSatisfied"></param>
    /// <param name="minimum">The minimum number of operand results that need to be satisfied (inclusive).</param>
    /// <param name="metadata">A function that generates metadata based on the overall satisfaction of the result.</param>
    /// <param name="operandResults">The collection of operand results.</param>
    internal AtLeastNSatisfiedBooleanResult(
        bool isSatisfied,
        int minimum,
        IEnumerable<TMetadata> metadata,
        IEnumerable<BooleanResultBase<TMetadata>> operandResults)
    {
        Minimum = minimum.ThrowIfLessThan(0, nameof(minimum));
        
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadata;
    }

    /// <summary>Gets the minimum number of operand results that need to be satisfied.</summary>
    public int Minimum { get; }

    /// <summary>Gets the substitute metadata associated with the boolean result.</summary>
    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    /// <summary>Gets the collection of operand results.</summary>
    public IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }

    /// <summary>
    ///     Gets the collection of determinative operand results that have the same satisfaction status as the overall
    ///     result.
    /// </summary>
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeResults => UnderlyingResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool IsSatisfied { get; }

    /// <summary>Gets the description of the boolean result.</summary>
    public override string Description
    {
        get
        {
            var satisfiedCount = UnderlyingResults.Count(result => result.IsSatisfied);
            var higherOrderStatement =
                $"AT_LEAST_{Minimum}{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";
                
            return DeterminativeResults.Any()
                ? $"{higherOrderStatement}({DeterminativeResults.Count()}x {Reasons.Humanize()})"
                : higherOrderStatement;
        }
    }

    /// <summary>Gets the reasons associated with the boolean result.</summary>
    public override IEnumerable<string> GatherReasons() => DeterminativeResults.SelectMany(r => r.GatherReasons());
}