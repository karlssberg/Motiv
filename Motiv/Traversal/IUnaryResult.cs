namespace Motiv.Traversal;

internal interface IUnaryOperationResult<TMetadata>
{
    BooleanResultBase<TMetadata> Operand { get; }
}
