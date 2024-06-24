using Motiv.HigherOrderProposition;

namespace Motiv;

/// <summary>
/// Extension methods for collections of <see cref="ModelResult{TModel}" />.
/// </summary>
public static class ModelResultExtensions
{
    /// <summary>
    /// Filters a collection of model-results, returning only those where the result is true.
    /// </summary>
    /// <param name="results">The collection of models to filter.</param>
    /// <typeparam name="TModel">The type of model.</typeparam>
    /// <returns>A collection of model-results where the result is true.</returns>
    public static IEnumerable<ModelResult<TModel>> WhereTrue<TModel>(
        this IEnumerable<ModelResult<TModel>> results) =>
        results.Where(result => result.Satisfied);
    
    /// <summary>
    /// Filters a collection of model-results, returning only those where the result is false.
    /// </summary>
    /// <param name="results">The collection of model-results to filter.</param>
    /// <typeparam name="TModel">The type of model.</typeparam>
    /// <returns>A collection of model-results where the result is false.</returns>
    public static IEnumerable<ModelResult<TModel>> WhereFalse<TModel>(
        this IEnumerable<ModelResult<TModel>> results) =>
        results.Where(result => result.Satisfied);
    
    /// <summary>
    /// Counts the number of model-results in a collection where the result is true.
    /// </summary>
    /// <param name="results">The collection of model-results to count.</param>
    /// <typeparam name="TModel">The type of model.</typeparam>
    /// <returns>The count of the model-results where the result is true.</returns>
    public static int CountTrue<TModel>(
        this IEnumerable<ModelResult<TModel>> results) =>
        results.Count(result => result.Satisfied);
    
    /// <summary>
    /// Counts the number of model-results in a collection where the result is false.
    /// </summary>
    /// <param name="results">The collection of model-results to count.</param>
    /// <typeparam name="TModel">The type of model.</typeparam>
    /// <returns>The count of the model-results where the result is false.</returns>
    public static int CountFalse<TModel>(
        this IEnumerable<ModelResult<TModel>> results) =>
        results.Count(result => !result.Satisfied);

    /// <summary>
    /// Checks if all model-results in a collection are true.
    /// </summary>
    /// <param name="results">The collection of model-results to check.</param>
    /// <typeparam name="TModel">The type of model.</typeparam>
    /// <returns>True if all model-results are true, false otherwise.</returns>
    public static bool AllTrue<TModel>(
        this IEnumerable<ModelResult<TModel>> results) =>
        results.All(result => result.Satisfied);

    /// <summary>
    /// Checks if all model-results in a collection are false.
    /// </summary>
    /// <param name="results">The collection of model-results to check.</param>
    /// <typeparam name="TModel">The type of model.</typeparam>
    /// <returns>True if all model-results are false, false otherwise.</returns>
    public static bool AllFalse<TModel>(
        this IEnumerable<ModelResult<TModel>> results) =>
        results.All(result => !result.Satisfied);

    /// <summary>
    /// Checks if any model-results in a collection are true.
    /// </summary>
    /// <param name="results">The collection of model-results to check.</param>
    /// <typeparam name="TModel">The type of model.</typeparam>
    /// <returns>True if any model-results is true, false otherwise.</returns>
    public static bool AnyTrue<TModel>(
        this IEnumerable<ModelResult<TModel>> results) =>
        results.Any(result => result.Satisfied);

    /// <summary>
    /// Checks if any model-results in a collection are false.
    /// </summary>
    /// <param name="results">The collection of model-results to check.</param>
    /// <typeparam name="TModel">The type of model.</typeparam>
    /// <returns>True if any model-results is false, false otherwise.</returns>
    public static bool AnyFalse<TModel>(
        this IEnumerable<ModelResult<TModel>> results) =>
        results.Any(result => !result.Satisfied);
}