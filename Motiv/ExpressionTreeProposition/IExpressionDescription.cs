namespace Motiv.ExpressionTreeProposition;

/// <summary>
/// Represents a description of an expression.
/// </summary>
/// <typeparam name="TModel">
/// The type of the model that the expression is based on.
/// </typeparam>
public interface IExpressionDescription<in TModel> : ISpecDescription
{
    /// <summary>
    /// Gets the expression.
    /// </summary>
    string ToAssertion(TModel model, bool satisfied);
}
