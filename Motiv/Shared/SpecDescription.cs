namespace Motiv.Shared;

internal sealed class SpecDescription(string statement, ISpecDescription? underlyingDescription = null) : ISpecDescription
{
    public string Statement => statement;

    public string Detailed => string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        yield return Statement;
        if (underlyingDescription is null)
            yield break;

        foreach (var line in underlyingDescription.GetDetailsAsLines())
            yield return line.Indent();
    }

    public string ToAssertion(bool satisfied) => Statement.ToReason(satisfied);


    public override string ToString() => Statement;
}
