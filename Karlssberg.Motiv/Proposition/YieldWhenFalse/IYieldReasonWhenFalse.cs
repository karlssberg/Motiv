using Karlssberg.Motiv.Proposition.Factories;

namespace Karlssberg.Motiv.Proposition.YieldWhenFalse;

/// <summary>Represents an interface for specifying the behavior when a condition is false.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public interface IYieldReasonWhenFalse<TModel>
{
    /// <summary>Specifies the behavior when the condition is false.</summary>
    /// <param name="falseBecause">The metadata associated with the condition.</param>
    /// <returns>The specification with the specified metadata.</returns>
    ISpecFactory<TModel> YieldWhenFalse(string falseBecause);


    /// <summary>Supply a function that when executed generates a human readable explanation for when the condition is false.</summary>
    /// <param name="falseBecause">
    ///     The function that evaluates the model and returns a human readable explanation of why the
    ///     predicate returned false.
    /// </param>
    /// <returns>A specification base.</returns>
    ISpecFactory<TModel> YieldWhenFalse(Func<TModel, string> falseBecause);
}