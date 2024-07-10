namespace Motiv;

internal interface IUnaryOperationResult<TMetadata>
{
    BooleanResultBase<TMetadata> Operand { get; }
}
