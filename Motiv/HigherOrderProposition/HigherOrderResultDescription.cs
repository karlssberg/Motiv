namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderResultDescription<TModel, TUnderlyingMetadata>(
    string reason,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes)
    : ResultDescriptionBase
{
    private readonly ICollection<BooleanResult<TModel, TUnderlyingMetadata>> _causes = causes.ToArray();
    internal override int CausalOperandCount => _causes.Count;

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


