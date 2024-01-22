using Humanizer;

namespace Karlssberg.Motiv.All;

/// <summary>Represents a boolean result that indicates whether all operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel">The model used to evaluate each underlying operand.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of metadata associated with each underlying operand.</typeparam>
public sealed class AllSatisfiedBooleanResult<TModel, TMetadata, TUnderlyingMetadata> : BooleanResultBase<TMetadata>, IAllSatisfiedBooleanResult<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="AllSatisfiedBooleanResult{TModel, TMetadata, TUnderlyingMetadata}" /> class.</summary>
    /// <param name="metadataFactory">
    ///     A function that creates metadata based on the overall satisfaction of the operand
    ///     results.
    /// </param>
    /// <param name="operandResults">The collection of operand results.</param>
    internal AllSatisfiedBooleanResult(
        Func<bool, IEnumerable<TMetadata>> metadataFactory,
        IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> operandResults)
    {
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        var isSatisfied = UnderlyingResults.All(result => result.IsSatisfied);

        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadataFactory(isSatisfied);
    }

    /// <summary>Gets the collection of operand results.</summary>
    public IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> UnderlyingResults { get; }

    /// <summary>Gets the determinative operand results that have the same satisfaction as the overall result.</summary>
    public IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> DeterminativeResults => UnderlyingResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    /// <summary>Gets the substitute metadata associated with the boolean result.</summary>
    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    IEnumerable<BooleanResultBase<TMetadata>> ICompositeBooleanResult<TMetadata>.UnderlyingResults => UnderlyingResults switch
    {
        IEnumerable<BooleanResultBase<TMetadata>> baseResults => baseResults,
        _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
    };

    IEnumerable<BooleanResultBase<TMetadata>> ICompositeBooleanResult<TMetadata>.DeterminativeResults => UnderlyingResults switch
    {
        IEnumerable<BooleanResultBase<TMetadata>> baseResults => baseResults
            .Where(result => result.IsSatisfied == IsSatisfied),
        _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
    };

    /// <inheritdoc cref="BooleanResultBase{TMetadata}.IsSatisfied" />
    public override bool IsSatisfied { get; }

    /// <inheritdoc cref="BooleanResultBase{TMetadata}.Description" />
    public override string Description
    {
        get
        {
            var satisfiedCount = UnderlyingResults.Count(result => result.IsSatisfied);
            var higherOrderStatement = $"ALL{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";
            return
                $"{higherOrderStatement}({DeterminativeResults.Count()}x {Reasons.Humanize()})";
        }
    }

    /// <inheritdoc />
    public override IEnumerable<string> GatherReasons() => DeterminativeResults.SelectMany(r => r.GatherReasons());
}