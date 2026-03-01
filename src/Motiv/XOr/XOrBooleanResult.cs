using Motiv.Shared;

namespace Motiv.XOr;

/// <summary>Represents the result of a logical XOR (exclusive OR) operation.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the result.</typeparam>
internal sealed class XOrBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : BinaryBooleanResult<TMetadata>(left, right)
{
    public override bool Satisfied { get; } = left.Satisfied ^ right.Satisfied;

    public override ResultDescriptionBase Description =>
        new XOrBooleanResultDescription<TMetadata>(Left, Right!);

    public override string Operation => Operator.XOr;

    public override bool IsCollapsable => false;

    protected override IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults() => GetAllResults();
}
