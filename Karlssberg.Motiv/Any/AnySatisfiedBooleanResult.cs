using Humanizer;

namespace Karlssberg.Motiv.Any;

/// <summary>Represents a boolean result that indicates whether any of the operand results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel">The type of the model used to evaluate each underlying operand. </typeparam>
public sealed class AnySatisfiedBooleanResult<TModel, TMetadata, TUnderlyingMetadata> : BooleanResultBase<TMetadata>, IAnySatisfiedBooleanResult<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="AnySatisfiedBooleanResult{TMetadata}" /> class.</summary>
    /// <param name="isSatisfied"></param>
    /// <param name="metadata">A function that creates metadata based on the boolean result.</param>
    /// <param name="operandResults">The operand results to evaluate.</param>
    internal AnySatisfiedBooleanResult(
        bool isSatisfied,
        IEnumerable<TMetadata> metadata,
        IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> operandResults)
    {
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();

        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadata;
    }

    /// <summary>Gets the operand results used to evaluate the boolean result.</summary>
    public IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> UnderlyingResults { get; }

    /// <summary>Gets the determinative operand results that have the same satisfaction status as the boolean result.</summary>
    public IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> DeterminativeResults => UnderlyingResults
        .Where(result => result.IsSatisfied == IsSatisfied);

    /// <summary>Gets the substitute metadata associated with the boolean result.</summary>
    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    IEnumerable<BooleanResultBase<TMetadata>> ICompositeBooleanResult<TMetadata>.UnderlyingResults => UnderlyingResults switch
    {
        IEnumerable<BooleanResultBase<TMetadata>> booleanResultBases => booleanResultBases,
        _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
    };

    /// <summary>Gets the determinative operand results that have the same satisfaction status as the boolean result.</summary>
    IEnumerable<BooleanResultBase<TMetadata>> ICompositeBooleanResult<TMetadata>.DeterminativeResults => UnderlyingResults switch
    {
        IEnumerable<BooleanResultBase<TMetadata>> booleanResultBases => booleanResultBases
            .Where(result => result.IsSatisfied == IsSatisfied),
        _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
    };

    /// <inheritdoc />
    public override bool IsSatisfied { get; }

    /// <inheritdoc />
    public override string Description => 
        $"ANY{{{DeterminativeResults.Count()}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}({DeterminativeResults.Distinct().Humanize()})";

    /// <inheritdoc />
    public override IEnumerable<string> GatherReasons() => DeterminativeResults.SelectMany(r => r.GatherReasons());
}