using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.DecoratorProposition.PropositionBuilders.Spec;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// </summary>
/// <param name="spec">The specification to use for the proposition.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct MultiAssertionSpecExplanationWithNamePropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, TMetadata> spec,
    [FluentMethod("WhenTrue")]string trueBecause,
    [FluentMethod("WhenFalseYield")]Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> falseBecause)
{
    private Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> TrueBecauseFunc =>
        trueBecause
            .ToEnumerable()
            .ToFunc<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>>();

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string statement) =>
        new SpecDecoratorMultiMetadataProposition<TModel, string, TMetadata>(
            spec,
            TrueBecauseFunc,
            falseBecause,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)), spec.Description));

    /// <summary>
    /// Creates a proposition with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create()
    {
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        return new SpecDecoratorMultiAssertionExplanationProposition<TModel, TMetadata>(
            spec,
            TrueBecauseFunc,
            falseBecause,
            new SpecDescription(trueBecause, spec.Description));
    }
}
