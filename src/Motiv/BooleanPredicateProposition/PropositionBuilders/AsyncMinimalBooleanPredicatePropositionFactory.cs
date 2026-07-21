using Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

/// <summary>A builder for creating asynchronous propositions based on an async predicate.</summary>
/// <param name="predicate">The async predicate function that evaluates the model to a boolean value.</param>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly partial struct AsyncMinimalBooleanPredicatePropositionFactory<TModel>(
    [MultipleFluentMethods(typeof(BuildAsyncOverloads))]Func<TModel, CancellationToken, ValueTask<bool>> predicate)
{
    /// <summary>Creates an asynchronous proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An asynchronous proposition for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>
    public AsyncPolicyBase<TModel, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new AsyncMinimalProposition<TModel>(
            predicate,
            new SpecDescription(statement));
    }
}
