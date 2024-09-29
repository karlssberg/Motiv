using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders.Metadata;

/// <summary>
/// A factory for creating propositions based on the supplied predicate and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
public readonly ref struct MetadataPropositionFactory<TModel, TMetadata>(
    Func<TModel, bool> predicate,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse)
{
    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, TMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new MetadataProposition<TModel, TMetadata>(
            predicate,
            whenTrue,
            whenFalse,
            new SpecDescription(statement));
    }
}
