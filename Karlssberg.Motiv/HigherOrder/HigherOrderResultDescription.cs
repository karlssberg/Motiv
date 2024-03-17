namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderResultDescription<TModel, TUnderlyingMetadata>(
    string because,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes )
    : ResultDescriptionBase
{
    private readonly ICollection<BooleanResult<TModel, TUnderlyingMetadata>> _causes = causes.ToArray();
    internal override int CausalOperandCount => _causes.Count;

    public override string Compact => because;

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
            var reasonFrequencyPair = _causes
                .OrderByDescending(result => result.Satisfied)
                .Select(result => result.Description.Compact)
                .GroupBy(reason => reason)
                .Select(grouping => (Reason: grouping.Key, Count: grouping.Count()))
                .OrderByDescending(grouping => grouping.Count)
                .ToArray();

            if (reasonFrequencyPair.Length == 0)
                return "";

            var indentSize = reasonFrequencyPair.First().Count.ToString().Length + 2;

            return reasonFrequencyPair
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


