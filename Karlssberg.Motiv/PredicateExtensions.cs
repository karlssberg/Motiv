using Karlssberg.Motiv.SpecBuilder;
using Karlssberg.Motiv.SpecBuilder.Phase1;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for predicates. These methods convert predicates into specifications.
/// </summary>
public static class PredicateExtensions
{
    /// <summary>
    /// Converts a predicate function into a SpecBuilder instance.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="predicate">The predicate function.</param>
    /// <returns>A new instance of SpecBuilder initialized with the specified predicate.</returns>
    public static IYieldReasonWhenTrue<TModel> ToSpec<TModel>(this Func<TModel, bool> predicate) => new SpecBuilder<TModel>(predicate);
}