using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderResultDescription<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    IEnumerable<TMetadata> metadataCollection,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes,
    IProposition proposition,
    AssertionSource assertionSource)
    : ResultDescriptionBase
{
    public ICollection<BooleanResult<TModel, TUnderlyingMetadata>> Causes { get; } = causes.ToArray();
    internal override int CausalOperandCount => Causes.Count;

    public override string Compact =>
        proposition.ToReason(isSatisfied, metadataCollection.SingleOrDefault(), assertionSource);

    public override string Detailed => GetDetails();

    private string GetDetails()
    {
        var causes = GetUnderlyingCauses();
        return causes switch
        {
            "" => Compact,
            _ =>
                $$"""
                  {{Compact}} {
                      {{causes.IndentAfterFirstLine()}}
                  }
                  """
        };

        string GetUnderlyingCauses()
        {
            var reasonFrequency = Causes
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