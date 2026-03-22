using Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

/// <summary>
/// A factory for creating propositions based on the supplied predicate and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
public readonly struct MultiAssertionExplanationPropositionFactory<TModel>
{
    private readonly Func<TModel, bool> _predicate;
    private readonly Func<TModel, IEnumerable<string>> _whenTrue;
    private readonly Func<TModel, IEnumerable<string>> _whenFalse;

    /// <summary>
    /// A factory for creating propositions based on the supplied predicate and metadata factories.
    /// </summary>
    /// <param name="predicate">The predicate to use for the specification.</param>
    /// <param name="whenTrue">The metadata factory for the proposition when the predicate is true.</param>
    /// <param name="whenFalse">The metadata factory for the proposition when the predicate is false.</param>
    [FluentConstructor(typeof(Spec), CreateMethod = CreateMethod.None)]
    public MultiAssertionExplanationPropositionFactory(
        [FluentMethod("Build")]Func<TModel, bool> predicate,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))] Func<TModel, IEnumerable<string>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))] Func<TModel, IEnumerable<string>> whenFalse)
    {
        predicate.ThrowIfNull(nameof(predicate));
        _predicate = predicate;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new MultiValueProposition<TModel, string>(
            _predicate,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement) { HasExplicitStatement = true });
    }
}
