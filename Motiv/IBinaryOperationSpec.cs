namespace Motiv;

/// <summary>
/// Represents a binary operation specification.
/// </summary>
/// <typeparam name="TModel">
/// The type of the model.
/// </typeparam>
public interface IBinaryOperationSpec : IBooleanOperationSpec
{
    /// <summary>
    /// The left side of the binary operation.
    /// </summary>
    SpecBase Left { get; }

    /// <summary>
    /// The right side of the binary operation.
    /// </summary>
    SpecBase Right { get; }
}

/// <summary>
/// Represents a binary operation specification.
/// </summary>
/// <typeparam name="TModel">
/// The type of the model.
/// </typeparam>
public interface IBinaryOperationSpec<TModel> : IBooleanOperationSpec
{
    /// <summary>
    /// The left side of the binary operation.
    /// </summary>
    SpecBase<TModel> Left { get; }

    /// <summary>
    /// The right side of the binary operation.
    /// </summary>
    SpecBase<TModel> Right { get; }
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
public interface IBinaryOperationSpec<TModel, TMetadata> : IBooleanOperationSpec
{
    /// <summary>
    /// The left side of the binary operation.
    /// </summary>
    SpecBase<TModel, TMetadata> Left { get; }

    /// <summary>
    /// The right side of the binary operation.
    /// </summary>
    SpecBase<TModel, TMetadata> Right { get; }
}
