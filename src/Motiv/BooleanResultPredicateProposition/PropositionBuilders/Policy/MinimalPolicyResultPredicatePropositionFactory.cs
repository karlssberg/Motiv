using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders.Policy;

/// <summary>
/// A builder for creating propositions using a predicate function that returns a
/// <see cref="PolicyResultBase{TMetadata}" />.
/// </summary>
/// <param name="predicate">The predicate function that evaluates the model to a <see cref="PolicyResultBase{TMetadata}" />.</param>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the underlying boolean result.</typeparam>
[FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
public readonly partial struct MinimalPolicyResultPredicatePropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(PolicyResultBuildOverloads))]Func<TModel, PolicyResultBase<TMetadata>> predicate)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, TMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new MinimalPolicyResultPredicateProposition<TModel, TMetadata>(
            predicate,
            (_, result) => result.Value,
            (_, result) => result.Value,
            new SpecDescription(statement) { HasExplicitStatement = true });
    }
}
