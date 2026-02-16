using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderResultDescription<TUnderlyingMetadata>(
    string reason,
    IEnumerable<string> additionalAssertions,
    IEnumerable<BooleanResultBase<TUnderlyingMetadata>> causes,
    string propositionStatement)
    : ResultDescriptionBase
{
    private readonly BooleanResultBase<TUnderlyingMetadata>[] _causes = causes.ToArray();

    internal override int CausalOperandCount => _causes.Length;

    internal override string Statement => propositionStatement;

    public override string Reason => reason;

    public override IEnumerable<string> GetJustificationAsLines()
    {
        yield return Reason;
        var distinctAssertions = additionalAssertions.DistinctWithOrderPreserved().ToArray();
        var assertionIndent = distinctAssertions.Length > 0 ? 1 : 0;
        foreach (var line in distinctAssertions)
            yield return line.Indent(assertionIndent);

        foreach (var line in GetUnderlyingJustificationsAsLines())
        {
            yield return line.Indent(assertionIndent + 1);
        }
    }

    private IEnumerable<string> GetUnderlyingJustificationsAsLines()
    {
        return _causes
                .DistinctWithOrderPreserved(result => result.Justification)
                .SelectMany(cause => cause.Description.GetJustificationAsLines());
    }
}


