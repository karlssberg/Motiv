﻿namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for predicates. These methods convert predicates into propositions.
/// </summary>
public static class SpecExtensions
{
    /// <summary>
    /// Converts a predicate function into a SpecBuilder instance.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="predicate">The predicate function.</param>
    /// <returns>A new instance of SpecBuilder initialized with the specified predicate.</returns>
    public static BooleanPredicateSpecBuilder<TModel> ToSpec<TModel>(this Func<TModel, bool> predicate) =>
        new (predicate);
}