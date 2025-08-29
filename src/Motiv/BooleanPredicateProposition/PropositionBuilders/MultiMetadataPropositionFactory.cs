using Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;
using Motiv.Generator.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

/// <summary>
/// A factory for creating propositions based on the supplied predicate and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
public readonly partial struct MultiMetadataPropositionFactory<TModel, TMetadata>
{
    private readonly Func<TModel, bool> _predicate;
    private readonly Func<TModel, IEnumerable<TMetadata>> _whenTrue;
    private readonly Func<TModel, IEnumerable<TMetadata>> _whenFalse;

    /// <summary>
    /// A factory for creating propositions based on the supplied predicate and metadata factories.
    /// </summary>
    /// <param name="predicate">The predicate to use for the specification.</param>
    /// <param name="whenTrue">The metadata factory for the proposition when the predicate is true.</param>
    /// <param name="whenFalse">The metadata factory for the proposition when the predicate is false.</param>
    [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiMetadataPropositionFactory(
        [FluentMethod("Build")]Func<TModel, bool> predicate,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))] Func<TModel, IEnumerable<TMetadata>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))] Func<TModel, IEnumerable<TMetadata>> whenFalse)
    {
        predicate.ThrowIfNull(nameof(predicate));
        _predicate = predicate;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

    /// <summary>
    /// A factory for creating propositions based on the supplied predicate and metadata factories.
    /// </summary>
    [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiMetadataPropositionFactory(
        [FluentMethod("Build")]Func<TModel, bool> predicate,
        [MultipleFluentMethods(typeof(WhenTrueOverloads))] Func<TModel, TMetadata> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, IEnumerable<TMetadata>> whenFalse)
    {
        predicate.ThrowIfNull(nameof(predicate));
        _predicate = predicate;
        _whenTrue = whenTrue.ToEnumerableReturn();
        _whenFalse = whenFalse;
    }

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, TMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new MultiValueProposition<TModel, TMetadata>(
            _predicate,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement));
    }
}
