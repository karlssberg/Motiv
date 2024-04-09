﻿namespace Karlssberg.Motiv.HigherOrderProposition.PropositionBuilders.Explanation;

/// <summary>
/// A factory for creating specifications based on a predicate and explanations for true and false conditions.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct ExplanationFromBooleanPredicateWithNameHigherOrderPropositionFactory<TModel>(
    Func<TModel, bool> predicate,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate, 
    Func<HigherOrderBooleanEvaluation<TModel>, string> trueBecause, 
    Func<HigherOrderBooleanEvaluation<TModel>, string> falseBecause,
    ISpecDescription candidateSpecDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>>? causeSelector)
{
    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create() =>
        new HigherOrderFromBooleanPredicateExplanationProposition<TModel>(
            predicate,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            candidateSpecDescription,
            causeSelector);

    /// <summary>
    /// Creates a specification with descriptive assertions, but using the supplied proposition to succinctly explain
    /// the decision.
    /// </summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string proposition)
    {
        proposition.ThrowIfNullOrWhitespace(nameof(proposition));
        return new HigherOrderFromBooleanPredicateMetadataProposition<TModel,string>(
            predicate,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            new SpecDescription(proposition),
            causeSelector);
    }
}