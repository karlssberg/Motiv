using System.Linq.Expressions;
using Motiv.Generator.Attributes;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// Creates a minimal proposition from an expression tree.
/// </summary>
/// <param name="expression">The expression tree predicate to represent</param>
/// <typeparam name="TModel">The model type</typeparam>
/// <typeparam name="TPredicateResult">The predicate type</typeparam>
[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct MinimalExpressionTreePropositionFactory<TModel, TPredicateResult>(
    [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string statement) =>
        new ExpressionTreeMultiMetadataProposition<TModel, string, TPredicateResult>(
            expression,
            (_, result) => result.Values,
            (_, result) => result.Values,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))));
}
