using Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv.HigherOrder;

internal class HigherOrderResultDescription<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    IEnumerable<TMetadata> metadataCollection,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes,
    IProposition proposition,
    ReasonSource reasonSource)
    : ResultDescriptionBase
{
    private readonly BooleanResult<TModel, TUnderlyingMetadata>[] _causes = causes.ToArray();
    internal override int CausalOperandCount => _causes.Length;
    public override string Compact => proposition.ToReason(isSatisfied, metadataCollection.SingleOrDefault(), reasonSource);
    public override string Detailed => GetFullDescription();

    private string GetFullDescription()
    {
        var details = GetDetails();
        return details switch
        {
            "" => Compact,
            _ => $"""
                  {Compact}
                      {details.IndentAfterFirstLine()}
                  """
        };
        
        string GetDetails()
        {
            var reasonFrequency = _causes
                .OrderByDescending(result => result.Satisfied)
                .Select(result => result.Description.Compact)
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
}