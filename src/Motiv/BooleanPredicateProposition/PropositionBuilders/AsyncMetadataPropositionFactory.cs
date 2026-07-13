using Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

/// <summary>
/// A factory for creating asynchronous propositions based on the supplied async predicate and metadata factories.
/// </summary>
/// <param name="predicate">The async predicate to use for the specification.</param>
/// <param name="whenTrue">The metadata factory for the proposition when the predicate is true.</param>
/// <param name="whenFalse">The metadata factory for the proposition when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct AsyncMetadataPropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(BuildAsyncOverloads))]Func<TModel, CancellationToken, Task<bool>> predicate,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, TMetadata> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, TMetadata> whenFalse)
{
    /// <summary>
    /// Creates an asynchronous proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An asynchronous proposition for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>
    public AsyncPolicyBase<TModel, TMetadata> Create(string statement)
    {
        predicate.ThrowIfNull(nameof(predicate));
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new AsyncProposition<TModel, TMetadata>(
            predicate,
            whenTrue,
            whenFalse,
            new SpecDescription(statement));
    }
}
