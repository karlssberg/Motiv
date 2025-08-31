using Motiv.FluentFactory.Generator;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Spec;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <param name="spec">The specification to decorate.</param>
/// <param name="higherOrderOperation">The higher-order predicate operation.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly struct MultiAssertionFromSpecWithNameHigherOrderPropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, TMetadata> spec,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation,
    [FluentMethod("WhenTrue")]string trueBecause,
    [FluentMethod("WhenFalseYield")]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<string>> falseBecause)
{
    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false, and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromBooleanResultMultiMetadataProposition<TModel, string, TMetadata>(
            spec.IsSatisfiedBy,
            higherOrderOperation.HigherOrderPredicate,
            trueBecause
                .ToEnumerable()
                .ToFunc<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<string>>(),
            falseBecause,
            new SpecDescription(statement, spec.Description),
            higherOrderOperation.CauseSelector);
    }

    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create() =>
        new HigherOrderFromBooleanResultMultiMetadataProposition<TModel, string, TMetadata>(
            spec.IsSatisfiedBy,
            higherOrderOperation.HigherOrderPredicate,
            trueBecause
                .ToEnumerable()
                .ToFunc<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<string>>(),
            falseBecause,
            new SpecDescription(trueBecause, spec.Description),
            higherOrderOperation.CauseSelector);
}
