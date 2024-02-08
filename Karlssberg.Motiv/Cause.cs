using System.Collections;

namespace Karlssberg.Motiv;

public record Cause(string Value, IEnumerable<Cause> UnderlyingCauses) : IEnumerable<string>
{
    private IEnumerable<string> AllCauses
    {
        get
        {
            yield return Value;
            foreach (var reason in UnderlyingCauses)
            {
                yield return reason.Value;
            }
        }
    }

    public IEnumerable<Cause> UnderlyingCauses { get; } = UnderlyingCauses;
    public string Value { get; } = Value;
    public IEnumerator<string> GetEnumerator() => AllCauses.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}