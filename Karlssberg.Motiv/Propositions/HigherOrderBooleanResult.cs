namespace Karlssberg.Motiv.Propositions;

internal class HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    IEnumerable<TMetadata> metadataCollection,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> operandResults,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes,
    string description) 
    : BooleanResultBase<TMetadata>
{
    public override MetadataSet<TMetadata> Metadata => new(metadataCollection);
        
    
    public override bool Satisfied => isSatisfied;
    
    public override string Description => GetFullDescription();
    
    public override IEnumerable<BooleanResultBase> UnderlyingResults { get; } =
        operandResults switch
        {
            IEnumerable<BooleanResultBase<TMetadata>> results => results,
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };

    public override Explanation Explanation => GetExplanation();

    public override Cause<TMetadata> Cause =>
        new (Metadata, Explanation.Reasons)
        {
            Underlying = causes switch
            {
                IEnumerable<BooleanResultBase<TMetadata>> results => results.Select(result => result.Cause),
                _ => Enumerable.Empty<Cause<TMetadata>>()
            }
        };
    
    private Explanation GetExplanation()
    {
        var underlyingReasons = causes
            .Select(cause => cause.Explanation);
        
        return metadataCollection switch
        {
            IEnumerable<string> reasons => new Explanation(GetShortDescription(reasons))
            {
                Underlying = underlyingReasons
            },
            _ => new Explanation($"'{description}' is {IsSatisfiedDisplayText()}")
            {
                Underlying = underlyingReasons
            }
        };
    }

    private string GetShortDescription(IEnumerable<string> reasons) => reasons.Count() switch
    {
        1 => reasons.First(),
        _ => $"'{description}' is {IsSatisfiedDisplayText()}"
    };
    private string GetFullDescription()
    {
        var trueCount = operandResults.CountTrue();
        var total = operandResults.Count();
        var reasons = SummarizeUnderlyingReasons();
        
        var higherOrderStatement = $"<{description}>{{{trueCount}/{total}}}:{IsSatisfiedDisplayText()}";
        return causes.Any()
            ? $"{higherOrderStatement}({reasons})"
            : higherOrderStatement;
    }
    
    private string SummarizeUnderlyingReasons()
    {
        var summaries = causes
            .OrderByDescending(result => result.Satisfied)
            .SelectMany(result => result.Explanation.Reasons)
            .GroupBy(reason => reason)
            .Select(grouping =>
            {
                var count = grouping.Count();
                return $"{grouping.Key} x{count}";
            })
            .ToArray();
            
        return summaries.Length switch
        {
            0 => "",
            1 => summaries.First(),
            _ => string.Join(", ", summaries)
        };
    }
}