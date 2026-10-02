namespace Motiv.Traversal;

internal interface IUnaryOperationResult;

internal interface IUnaryOperationResult<TMetadata> : IUnaryOperationResult
{
    BooleanResultBase<TMetadata> Operand { get; }
}
