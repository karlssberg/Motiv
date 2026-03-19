using Motiv.FluentFactory.Attributes;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders;

/// <summary>A builder for creating propositions based on a predicate, or for further refining a proposition.</summary>
/// <param name="predicate">The predicate function that evaluates the model to a boolean value.</param>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
[FluentConstructor(typeof(Spec), CreateMethod = CreateMethod.None)]
public readonly partial struct MinimalBooleanPredicatePropositionFactory<TModel>(
    [FluentMethod("Build")]Func<TModel, bool> predicate)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new MinimalProposition<TModel>(
            predicate,
            new SpecDescription(statement) { HasExplicitStatement = true });
    }
}
