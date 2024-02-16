using Humanizer;

namespace Karlssberg.Motiv.Proposition;

internal class HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    IEnumerable<TMetadata> metadataCollection,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> operandResults,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes,
    string description) 
    : BooleanResultBase<TMetadata>
{
    public IEnumerable<TMetadata> Metadata => metadataCollection;
    public override bool Satisfied => isSatisfied;
    public override string Description => GetFullDescription();
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; } =
        operandResults switch
        {
            IEnumerable<BooleanResultBase<TMetadata>> results => results,
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };
    
    public override IEnumerable<BooleanResultBase<TMetadata>> Causes { get; } = causes
        switch
        {
            IEnumerable<BooleanResultBase<TMetadata>> results => results,
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };


    public override IEnumerable<Reason> ReasonHierarchy => GetReasonHierarchy();

    private IEnumerable<Reason> GetReasonHierarchy() =>
        [metadataCollection switch
        {
            IEnumerable<string> reasons => new Reason(
                GetShortDescription(reasons),
                causes.SelectMany(cause => cause.ReasonHierarchy)),
            _ => new Reason(
                $"'{description}' is {IsSatisfiedDisplayText}", 
                causes.SelectMany(cause => cause.ReasonHierarchy))
        }];

    private string GetShortDescription(IEnumerable<string> reasons) => reasons.Count() switch
    {
        1 => reasons.First(),
        _ => $"'{description}' is {IsSatisfiedDisplayText}"
    };
    private string GetFullDescription()
    {
        var trueCount = operandResults.CountTrue();
        var total = operandResults.Count();
        var reasons = SummarizeUnderlyingReasons();
        
        var higherOrderStatement = $"<{description}>{{{trueCount}/{total}}}:{IsSatisfiedDisplayText}";
        return causes.Any()
            ? $"{higherOrderStatement}({reasons})"
            : higherOrderStatement;
    }
    
    private string SummarizeUnderlyingReasons()
    {
        var summaries = causes
            .OrderByDescending(result => result.Satisfied)
            .SelectMany(result => result.Reasons)
            .GroupBy(reason => reason)
            .Select(grouping =>
            {
                var count = grouping.Count();
                return $"{grouping.Key} x{count}";
            })
            .ToArray();
            
        return summaries.Length switch
        {
            0 => "no reason",
            1 => summaries.First(),
            _ => string.Join(", ", summaries)
        };
    }
}