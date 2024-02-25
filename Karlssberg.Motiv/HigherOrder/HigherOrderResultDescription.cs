using Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv.HigherOrder;

internal class HigherOrderResultDescription<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    IEnumerable<TMetadata> metadataCollection,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes,
    IProposition proposition,
    ReasonSource reasonSource)
    : IResultDescription
{
    private readonly BooleanResult<TModel, TUnderlyingMetadata>[] _causes = causes.ToArray();
    public int CausalOperandCount => _causes.Length;
    public string Reason => proposition.ToReason(isSatisfied, metadataCollection.SingleOrDefault(), reasonSource);
    public string Details => GetFullDescription();

    private string GetFullDescription()
    {
        var details = GetDetails();
        return details switch
        {
            "" => Reason,
            _ => $"""
                  {Reason}
                      {details.IndentAfterFirstLine()}
                  """
        };
        
        string GetDetails()
        {
            var reasonFrequency = _causes
                .OrderByDescending(result => result.Satisfied)
                .Select(result => result.Description.Reason)
                .GroupBy(reason => reason)
                .Select(grouping => (Reason: grouping.Key, Count: grouping.Count()))
                .OrderByDescending(grouping => grouping.Count)
                .ToArray();

            if (reasonFrequency.Length == 0)
                return "";
            
            var indentSize = reasonFrequency.First().Count.ToString().Length + 2;
            
            return reasonFrequency
                .Select(item => 
                {
                    var (reason, count) = item;
                    var countAsText = $"{count}x ".PadRight(indentSize);
                    
                    return $"{countAsText}{reason.IndentAfterFirstLine()}";
                })
                .JoinLines();
        }
    }
    
    public override string ToString() => Reason;
}