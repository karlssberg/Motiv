using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTrees.PropositionBuilders.Metadata;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
public readonly ref struct MetadataPropositionFactory<TModel, TMetadata>(
    Expression<Func<TModel, bool>> expression,
    Func<TModel, BooleanResultBase<string>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<string>, TMetadata> whenFalse)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, TMetadata> Create(string statement) =>
        new ExpressionTreeMetadataProposition<TModel, TMetadata>(
            expression,
            whenTrue,
            whenFalse,
            statement.ThrowIfNullOrWhitespace(nameof(statement)));
}