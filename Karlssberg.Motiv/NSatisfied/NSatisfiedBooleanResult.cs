using Humanizer;

namespace Karlssberg.Motiv.NSatisfied;

/// <summary>Represents a boolean result that is satisfied if n underlying results are satisfied.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
/// <typeparam name="TModel"></typeparam>
/// <typeparam name="TUnderlyingMetadata"></typeparam>
public sealed class NSatisfiedBooleanResult<TModel, TMetadata, TUnderlyingMetadata> : 
    BooleanResultBase<TMetadata>
{
    private readonly string _name;

    /// <summary>Initializes a new instance of the <see cref="NSatisfiedBooleanResult{TModel,TMetadata,TUnderlyingMetadata}" /> class.</summary>
    /// <param name="name"></param>
    /// <param name="isSatisfied"></param>
    /// <param name="metadata"></param>
    /// <param name="operandResults">The collection of operand results.</param>
    internal NSatisfiedBooleanResult( 
        string name,
        bool isSatisfied,
        IEnumerable<TMetadata> metadata,
        IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> operandResults)
    {
        _name = name;
        UnderlyingResults = operandResults
            .ThrowIfNull(nameof(operandResults))
            .ToArray();
        
        IsSatisfied = isSatisfied;
        SubstituteMetadata = metadata;
    }


    /// <summary>Gets the substitute metadata associated with the boolean result.</summary>
    public IEnumerable<TMetadata> SubstituteMetadata { get; }

    
    /// <summary>Gets the collection of operand results.</summary>
    public IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>> UnderlyingResults { get; }

    /// <summary>
    ///     Gets the collection of determinative operand results that have the same satisfaction status as the overall
    ///     result.
    /// </summary>
    public IEnumerable<BooleanResultBase<TUnderlyingMetadata>> DeterminativeResults => UnderlyingResults
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
                $"{_name}{{{satisfiedCount}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";
                
            return DeterminativeResults.Any()
                ? $"{higherOrderStatement}({DeterminativeResults.Count()}x {Reasons.Humanize()})"
                : higherOrderStatement;
        }
    }

    /// <summary>Gets the reasons associated with the boolean result.</summary>
    public override IEnumerable<string> GatherReasons() => DeterminativeResults
        .SelectMany(r => r.GatherReasons());
}