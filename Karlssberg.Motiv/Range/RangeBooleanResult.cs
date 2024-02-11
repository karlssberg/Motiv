namespace Karlssberg.Motiv.Range;

internal class RangeBooleanResult<TModel, TMetadata>(
    bool isSatisfied,
    int minimum,
    int maximum,
    IReadOnlyCollection<BooleanResultBase<TMetadata>> underlyingResults)
    : BooleanResultBase<TMetadata>
{
    public override bool Satisfied => isSatisfied;
    
    
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults => underlyingResults;
    
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => Satisfied switch
    {
        true => UnderlyingResults.Where(result => result.Satisfied == Satisfied),
        false => UnderlyingResults
    };

    public override string Description => GetDescription();
    private string GetDescription()
    {
        var higherOrderStatement =
            $"BETWEEN_{minimum}_AND_{maximum}{{{DeterminativeOperands.Count()}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";

        return DeterminativeOperands.Any()
            ? $"{higherOrderStatement}({ReasonHierarchy.SummarizeReasons()})"
            : higherOrderStatement;
    }

    public override IEnumerable<Reason> ReasonHierarchy => DeterminativeOperands
        .SelectMany(result => result.ReasonHierarchy);
}