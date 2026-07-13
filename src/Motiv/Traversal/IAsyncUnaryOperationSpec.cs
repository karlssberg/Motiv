namespace Motiv.Traversal;

/// <summary>Represents an asynchronous unary operation specification.</summary>
internal interface IAsyncUnaryOperationSpec : IBooleanOperationSpec
{
    /// <summary>The operand of the unary operation.</summary>
    SpecBase Operand { get; }
}
