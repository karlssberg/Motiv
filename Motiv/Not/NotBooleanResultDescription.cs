namespace Motiv.Not;

internal sealed class NotBooleanResultDescription<TMetadata>(BooleanResultBase operand) : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;
    public override string Reason => operand.Description.Reason;
    
    public override IEnumerable<string> GetJustificationAsLines() => FormatDetails();

    private IEnumerable<string> FormatDetails() =>
        operand.Description
            .GetJustificationAsLines();
}