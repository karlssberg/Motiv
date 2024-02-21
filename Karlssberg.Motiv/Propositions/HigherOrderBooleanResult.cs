namespace Karlssberg.Motiv.Propositions;

internal sealed class HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    IEnumerable<TMetadata> metadataCollection,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> operandResults,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes,
    string description)
    : BooleanResultBase<TMetadata>
{
    public override MetadataSet<TMetadata> Metadata => new(metadataCollection);


    public override bool Satisfied => isSatisfied;

    public override string Description => SummarizeMetadata();

    public override Explanation Explanation => GetExplanation();

    public override Cause<TMetadata> Cause =>
        new(Metadata, Explanation.Reasons)
        {
            Underlying = causes switch
            {
                IEnumerable<BooleanResultBase<TMetadata>> results => results.Select(result => result.Cause),
                _ => Enumerable.Empty<Cause<TMetadata>>()
            }
        };

    private Explanation GetExplanation() =>
        new(SummarizeMetadata())
        {
            Underlying = causes
                .Select(cause => cause.Explanation)
        };

    private string SummarizeMetadata()
    {
        return metadataCollection switch
        {
            IEnumerable<string> reasons => SummarizeReasons(reasons),
            _ => GetDefaultResult(description)
        };
    }

    private string SummarizeReasons(IEnumerable<string> reasons) => reasons.SingleOrDefault() ?? GetDefaultResult(description);

    private string GetDefaultResult(string description) => $"'{description}' is {IsSatisfiedDisplayText()}";

    private string GetDescription()
    {
        return causes.Any()
            ? $"{GetHigherOrderStatement()}({SummarizeUnderlyingReasons()})"
            : GetHigherOrderStatement();
        
        string GetHigherOrderStatement()
        {
            var trueCount = operandResults.CountTrue();
            var total = operandResults.Count();
            return $"<{description}>{{{trueCount}/{total}}}:{IsSatisfiedDisplayText()}";
        }
    }

    internal override string DebuggerDisplay()
    {
        return GetDescription();
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