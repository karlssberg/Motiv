﻿using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders.Explanation;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TPredicateResult">The return type of the predicate expression.</typeparam>
public readonly ref struct MultiAssertionExplanationWithNameExpressionTreePropositionFactory<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    string trueBecause,
    Func<TModel, BooleanResultBase<string>, IEnumerable<string>> falseBecause)
{
    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string statement) =>
        new ExpressionTreeMultiMetadataProposition<TModel, string, TPredicateResult>(
            expression,
            trueBecause
                .ToEnumerable()
                .ToFunc<TModel, BooleanResultBase<string>, IEnumerable<string>>(),
            falseBecause,
            new OverridingExpressionDescription<TModel>(statement.ThrowIfNullOrWhitespace(nameof(statement))));

    /// <summary>
    /// Creates a proposition with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create() =>
        new ExpressionTreeMultiMetadataProposition<TModel, string, TPredicateResult>(
            expression,
            trueBecause
                .ToEnumerable()
                .ToFunc<TModel, BooleanResultBase<string>, IEnumerable<string>>(),
            falseBecause,
            new ExpressionTreeDescription<TModel, TPredicateResult>(expression));
}