namespace Motiv.HigherOrderProposition;

/// <summary>
/// Represents the result of a model evaluation.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public record ModelResult<TModel>(TModel Model, bool Satisfied)
{
    /// <summary>
    /// Gets the model.
    /// </summary>
    /// <value>The model.</value>
    public TModel Model { get; } = Model;

    /// <summary>
    /// Gets a value indicating whether the model satisfies a certain condition.
    /// </summary>
    /// <value><c>true</c> if the model is satisfied; otherwise, <c>false</c>.</value>
    public bool Satisfied { get; } = Satisfied;
}