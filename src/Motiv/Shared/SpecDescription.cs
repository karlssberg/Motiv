namespace Motiv.Shared;

internal sealed class SpecDescription(string statement, ISpecDescription? underlyingDescription = null) : ISpecDescription
{
    private string? _satisfiedReason;
    private string? _unsatisfiedReason;

    public string Statement => statement;

    public string Detailed => field ??= string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        yield return Statement;
        if (underlyingDescription is null)
            yield break;

        foreach (var line in underlyingDescription.GetDetailsAsLines())
            yield return line.Indent();
    }

    public string ToReason(bool satisfied) => satisfied
        ? _satisfiedReason ??= Statement.ToReason(true)
        : _unsatisfiedReason ??= Statement.ToReason(false);

    public override string ToString() => Statement;
}
