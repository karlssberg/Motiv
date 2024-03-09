namespace Karlssberg.Motiv.Not;

/// <summary>Represents the result of a logical NOT operation on a boolean result.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the result.</typeparam>
internal sealed class NotBooleanResult<TMetadata>(BooleanResultBase<TMetadata> operandResult) 
    : BooleanResultBase<TMetadata>
{

    /// <summary>Gets a value indicating whether the negation is satisfied.</summary>
    public override bool Satisfied => !operandResult.Satisfied;

    /// <summary>Gets the description of the negation result.</summary>
    public override ResultDescriptionBase Description => new NotBooleanResultDescription(operandResult);

    /// <summary>Gets the reasons associated with the operand result.</summary>
    public override Explanation Explanation => operandResult.Explanation;

    public override MetadataSet<TMetadata> Metadata => operandResult.Metadata;
    public override IEnumerable<BooleanResultBase> Underlying => operandResult.ToEnumerable();
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => operandResult.ToEnumerable();
    public override IEnumerable<BooleanResultBase> Causes => operandResult.ToEnumerable();
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata => operandResult.ToEnumerable();
}