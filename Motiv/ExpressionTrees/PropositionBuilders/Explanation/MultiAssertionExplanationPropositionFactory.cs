using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTrees.PropositionBuilders.Explanation;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create
/// a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly ref struct MultiAssertionExplanationPropositionFactory<TModel>(
    Expression<Func<TModel, bool>> expression,
    Func<TModel, BooleanResultBase<string>, IEnumerable<string>> trueBecause,
    Func<TModel, BooleanResultBase<string>, IEnumerable<string>> falseBecause)
{
    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string statement) =>
        new ExpressionTreeMultiMetadataProposition<TModel, string>(
            expression,
            trueBecause,
            falseBecause,
            statement.ThrowIfNullOrWhitespace(nameof(statement)));
}
