using Humanizer;

namespace Karlssberg.Motiv.Between;

internal class BetweenBooleanResult<TModel, TMetadata>(
    bool isSatisfied,
    int minimum,
    int maximum,
    IEnumerable<BooleanResultWithModel<TModel, TMetadata>> underlyingResults)
    : BooleanResultBase<TMetadata>
{
    public override bool Value => isSatisfied;
    
    
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults => underlyingResults;
    
    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands => Value switch
    {
        true => UnderlyingResults.Where(result => result.Value == Value),
        false => UnderlyingResults
    };

    public override string Description => GetDescription();
    private string GetDescription()
    {
        var higherOrderStatement =
            $"BETWEEN_{minimum}_AND_{maximum}{{{DeterminativeOperands.Count()}/{UnderlyingResults.Count()}}}:{IsSatisfiedDisplayText}";

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
    
    public override IEnumerable<string> GatherReasons() => DeterminativeOperands
        .SelectMany(result => result.GatherReasons());
}