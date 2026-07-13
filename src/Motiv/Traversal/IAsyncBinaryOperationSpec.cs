namespace Motiv.Traversal;

/// <summary>Represents an asynchronous binary operation specification.</summary>
internal interface IAsyncBinaryOperationSpec : IBooleanOperationSpec
{
    /// <summary>The left side of the binary operation.</summary>
    SpecBase Left { get; }

    /// <summary>The right side of the binary operation.</summary>
    SpecBase Right { get; }
}

/// <summary>Represents an asynchronous binary operation specification.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal interface IAsyncBinaryOperationSpec<TModel, TMetadata> : IAsyncBinaryOperationSpec
{
    /// <summary>The left side of the binary operation.</summary>
    new AsyncSpecBase<TModel, TMetadata> Left { get; }

    /// <summary>The right side of the binary operation.</summary>
    new AsyncSpecBase<TModel, TMetadata> Right { get; }
}
