using Motiv.Generator.Attributes;
using Motiv.Shared;

namespace Motiv.SpecDecoratorProposition.PropositionBuilders.Explanation;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct ExplanationPolicyDecoratorFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(PolicyBuildOverloads))]PolicyBase<TModel, TMetadata> policy,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, PolicyResultBase<TMetadata>, string> trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, PolicyResultBase<TMetadata>, string> falseBecause)
{
    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, string> Create(string statement) =>
        new PolicyDecoratorExplanationProposition<TModel, TMetadata>(
            policy,
            trueBecause,
            falseBecause,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)), policy.Description));
}
