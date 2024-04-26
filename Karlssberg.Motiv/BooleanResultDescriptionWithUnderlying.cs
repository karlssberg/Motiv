namespace Karlssberg.Motiv;

internal sealed class BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    string reason)
    : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;

    public override string Reason => reason;

    public override IEnumerable<string> GetDetailsAsLines()
    {
        if (IsReasonTheSameAsUnderlying())
        {
            foreach (var line in booleanResult.Description.GetDetailsAsLines())
                yield return line;
            
            yield break;
        }
        
        yield return reason;
        foreach (var line in booleanResult.Description.GetDetailsAsLines())
            yield return line.IndentLine();
    }

    private bool IsReasonTheSameAsUnderlying() => reason == booleanResult.Description.Reason;
}