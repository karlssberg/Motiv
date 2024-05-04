namespace Motiv;

internal sealed class BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
    BooleanResultBase booleanResult,
    string reason)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;

    public override string Reason => reason;

    public override IEnumerable<string> GetJustificationAsLines()
    {
        if (IsReasonTheSameAsUnderlying())
        {
            foreach (var line in booleanResult.Description.GetJustificationAsLines())
                yield return line;
            
            yield break;
        }
        
        yield return reason;
        foreach (var line in booleanResult.Description.GetJustificationAsLines())
            yield return line.Indent();
    }

    private bool IsReasonTheSameAsUnderlying() => reason == booleanResult.Description.Reason;
}