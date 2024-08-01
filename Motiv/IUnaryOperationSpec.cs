namespace Motiv;


/// <summary>
/// Represents a binary operation specification.
/// </summary>
/// <typeparam name="TModel">
/// The type of the model.
/// </typeparam>
public interface IUnaryOperationSpec : IBooleanOperationSpec
{
    /// <summary>
    /// The underlying operand.
    /// </summary>
    SpecBase Operand { get; }
}

/// <summary>
/// Represents a binary operation specification.
/// </summary>
/// <typeparam name="TModel">
/// The type of the model.
/// </typeparam>
public interface IUnaryOperationSpec<TModel> : IBooleanOperationSpec
{
    /// <summary>
    /// The underlying operand.
    /// </summary>
    SpecBase<TModel> Operand { get; }
}

/// <summary>
/// Represents a binary operation specification.
/// </summary>
/// <typeparam name="TModel">
/// The type of the model.
/// </typeparam>
/// <typeparam name="TMetadata">
/// The type of the metadata.
/// </typeparam>
public interface IUnaryOperationSpec<TModel, TMetadata> : IBooleanOperationSpec
{
    /// <summary>
    /// The underlying operand.
    /// </summary>
    SpecBase<TModel, TMetadata> Operand { get; }
}
