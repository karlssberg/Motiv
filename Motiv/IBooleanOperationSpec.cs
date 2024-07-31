namespace Motiv;

/// <summary>
/// Represents a binary operation specification.
/// </summary>
public interface IBooleanOperationSpec
{
    /// <summary>
    /// The description of the binary operation.
    /// </summary>
    ISpecDescription Description { get; }
    
    /// <summary>
    /// The operation that the binary operation represents.
    /// </summary>
    string Operation { get; }
    
    /// <summary>
    /// Indicates whether the binary operation can be collapsed into a multi-operand operation. 
    /// </summary>
    bool IsCollapsable { get; }
}
