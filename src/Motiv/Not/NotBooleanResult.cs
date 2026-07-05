using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.Not;

/// <summary>Represents the result of a logical NOT operation on a boolean result.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the result.</typeparam>
internal sealed class NotBooleanOperationResult<TMetadata>(BooleanResultBase<TMetadata> operandResult)
    : BooleanResultBase<TMetadata>, IBooleanOperationResult<TMetadata>, IUnaryOperationResult<TMetadata>
{
    private readonly BooleanResultBase<TMetadata>[] _operandResults = [operandResult];

    public BooleanResultBase<TMetadata> Operand => operandResult;

    /// <summary>Gets a value indicating whether the negation is satisfied.</summary>
    public override bool Satisfied { get; } = !operandResult.Satisfied;

    /// <summary>Gets the description of the negation result.</summary>
    public override ResultDescriptionBase Description => field ??= new NotBooleanResultDescription<TMetadata>(Operand);

    /// <summary>Gets the reasons associated with the operand result.</summary>
    public override Explanation Explanation => Operand.Explanation;

    public override MetadataNode<TMetadata> MetadataTier => Operand.MetadataTier;

    public override IEnumerable<BooleanResultBase> Underlying => _operandResults;

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues => _operandResults;

    public override IEnumerable<BooleanResultBase> Causes => _operandResults;

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => _operandResults;
}
