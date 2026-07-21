using Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

/// <summary>
/// A factory for creating asynchronous propositions based on the supplied async predicate and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
public readonly struct AsyncMultiAssertionExplanationPropositionFactory<TModel>
{
    private readonly Func<TModel, CancellationToken, ValueTask<bool>> _predicate;
    private readonly Func<TModel, IEnumerable<string>> _whenTrue;
    private readonly Func<TModel, IEnumerable<string>> _whenFalse;

    /// <summary>
    /// A factory for creating asynchronous propositions based on the supplied async predicate and metadata factories.
    /// </summary>
    /// <param name="predicate">The async predicate to use for the specification.</param>
    /// <param name="whenTrue">The metadata factory for the proposition when the predicate is true.</param>
    /// <param name="whenFalse">The metadata factory for the proposition when the predicate is false.</param>
    [FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
    public AsyncMultiAssertionExplanationPropositionFactory(
        [MultipleFluentMethods(typeof(BuildAsyncOverloads))]Func<TModel, CancellationToken, ValueTask<bool>> predicate,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))] Func<TModel, IEnumerable<string>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))] Func<TModel, IEnumerable<string>> whenFalse)
    {
        predicate.ThrowIfNull(nameof(predicate));
        _predicate = predicate;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

    /// <summary>
    /// Creates an asynchronous proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable. Because a name is supplied, the <c>WhenTrue</c>/<c>WhenFalse</c> values are surfaced via <see cref="BooleanResultBase{TMetadata}.Values"/>, not <see cref="BooleanResultBase.Assertions"/>.</remarks>
    /// <returns>An asynchronous proposition for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>
    public AsyncSpecBase<TModel, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new AsyncMultiValueProposition<TModel, string>(
            _predicate,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement));
    }
}
