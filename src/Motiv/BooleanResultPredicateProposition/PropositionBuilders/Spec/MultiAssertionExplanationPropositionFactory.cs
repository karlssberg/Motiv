using Motiv.BooleanResultPredicateProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders.Spec;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly struct MultiAssertionExplanationPropositionFactory<TModel, TMetadata>
{
    private readonly Func<TModel, BooleanResultBase<TMetadata>> _predicate;
    private readonly Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> _trueBecause;
    private readonly Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> _falseBecause;

    /// <summary>
    /// A factory for creating propositions based on the supplied proposition and explanation factories.
    /// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    /// <param name="predicate">The predicate to use for the specification.</param>
    /// <param name="trueBecause">The explanation for when the predicate is true.</param>
    /// <param name="falseBecause">The explanation for when the predicate is false.</param>
    [FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
    public MultiAssertionExplanationPropositionFactory(
        [MultipleFluentMethods(typeof(BooleanResultBuildOverloads))]Func<TModel, BooleanResultBase<TMetadata>> predicate,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> trueBecause,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> falseBecause)
    {
        _predicate = predicate;
        _trueBecause = trueBecause;
        _falseBecause = falseBecause;
    }

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable. Because a name is supplied, the <c>WhenTrue</c>/<c>WhenFalse</c> values are surfaced via <see cref="BooleanResultBase{TMetadata}.Values"/>, not <see cref="BooleanResultBase.Assertions"/>.</remarks>
    /// <returns>A proposition for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>
    public SpecBase<TModel, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new BooleanResultPredicateMultiValueProposition<TModel, string, TMetadata>(
            _predicate,
            _trueBecause,
            _falseBecause,
            new SpecDescription(statement)
        );
    }
}
