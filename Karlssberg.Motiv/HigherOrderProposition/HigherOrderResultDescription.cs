namespace Karlssberg.Motiv.HigherOrderProposition;

internal sealed class HigherOrderResultDescription<TModel, TUnderlyingMetadata>(
    string reason,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes)
    : ResultDescriptionBase
{
    private readonly ICollection<BooleanResult<TModel, TUnderlyingMetadata>> _causes = causes.ToArray();
    internal override int CausalOperandCount => _causes.Count;

    public override string Reason => reason;

    public override string Detailed => GetDetails();

    private string GetDetails()
    {
        var causes = GetUnderlyingCauses();
        return causes switch
        {
            "" => Reason,
            _ =>
                $$"""
                  {{Reason}} {
                      {{causes.IndentAfterFirstLine()}}
                  }
                  """
        };

        string GetUnderlyingCauses()
        {
            var reasonFrequencyPair = _causes
                .OrderByDescending(result => result.Satisfied)
                .Select(result => result.Description.Reason)
                .GroupBy(causeReason => causeReason)
                .Select(grouping => (Reason: grouping.Key, Count: grouping.Count()))
                .OrderByDescending(grouping => grouping.Count)
                .ToArray();

            if (reasonFrequencyPair.Length == 0)
                return "";

            var indentSize = reasonFrequencyPair.First().Count.ToString().Length + 2;

            return reasonFrequencyPair
                .Select(item =>
                {
                    var (itemReason, count) = item;
                    var countAsText = $"{count}x ".PadRight(indentSize);

                    return $"{countAsText}{itemReason.IndentAfterFirstLine()}";
                })
                .JoinLines();
        }
    }
}


