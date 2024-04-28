namespace Karlssberg.Motiv.Not;

internal sealed class NotBooleanResultDescription<TMetadata>(BooleanResultBase operand) : ResultDescriptionBase
{
    internal override int CausalOperandCount => 1;
    public override string Reason => operand.Description.Reason;
    
    public override IEnumerable<string> GetDetailsAsLines() => FormatDetails();

    private IEnumerable<string> FormatDetails() =>
        operand.Description
            .GetDetailsAsLines();
}