using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderResultDescription<TUnderlyingMetadata>(
    string reason,
    IEnumerable<string> additionalAssertions,
    IEnumerable<BooleanResultBase<TUnderlyingMetadata>> causes,
    string propositionStatement)
    : ResultDescriptionBase
{
    private readonly ICollection<BooleanResultBase<TUnderlyingMetadata>> _causes = causes.ToArray();

    internal override int CausalOperandCount => _causes.Count;

    internal override string Statement => propositionStatement;

    public override string Reason => reason;

    public override IEnumerable<string> GetJustificationAsLines()
    {
        yield return Reason;
        var distinctWithOrderPreserved = additionalAssertions.DistinctWithOrderPreserved().ToArray();
        foreach (var line in distinctWithOrderPreserved)
            yield return line.Indent();

        foreach (var line in GetUnderlyingJustificationsAsLines())
        {
            if (distinctWithOrderPreserved.Length > 0)
                yield return line.Indent();
            else
                yield return line;
        }
    }

    private IEnumerable<string> GetUnderlyingJustificationsAsLines()
    {
        foreach (var line in UnderlyingDetailsAsLines())
            yield return line.Indent();

        yield break;

        IEnumerable<string> UnderlyingDetailsAsLines()
        {
            return _causes
                .DistinctWithOrderPreserved(result => result.Justification)
                .SelectMany(cause => cause.Description.GetJustificationAsLines());
        }
    }
}


