﻿namespace Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Explanation;

/// <summary>
/// A factory for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct ExplanationCompositeFactorySpecFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause)
{
    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false, and names it with the propositional statement provided.
    /// </summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="CompositeFactoryExplanationSpec{TModel, TUnderlyingMetadata}" />.</returns>
    public SpecBase<TModel, string> Create(string proposition) =>
        new CompositeFactoryExplanationSpec<TModel, TUnderlyingMetadata>(
            specPredicate,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}