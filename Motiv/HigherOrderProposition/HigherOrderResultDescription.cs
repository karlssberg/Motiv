namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderResultDescription<TUnderlyingMetadata>(
    string reason,
    IEnumerable<BooleanResultBase<TUnderlyingMetadata>> causes)
    : ResultDescriptionBase
{
    private readonly ICollection<BooleanResultBase<TUnderlyingMetadata>> _causes = causes.ToArray();
    internal override int CausalOperandCount => _causes.Count;
    internal override string Statement { get; }

    public override string Reason => reason;

    public override IEnumerable<string> GetJustificationAsLines()
    {
        yield return Reason;
        foreach (var line in UnderlyingDetailsAsLines())
            yield return line.Indent();

        yield break;

        IEnumerable<string> UnderlyingDetailsAsLines()
        {
            return _causes
                .SelectMany(cause => cause.Description.GetJustificationAsLines());
        }
    }
}


