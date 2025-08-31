using Motiv.Generator;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Spec;

/// <summary>
/// A factory for creating specifications based on a predicate and explanations for true and false conditions.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <param name="spec">The specification to evaluate.</param>
/// <param name="higherOrderOperation">The higher-order predicate operation.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the specification.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly struct ExplanationFromSpecHigherOrderPropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, TMetadata> spec,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation,
    [MultipleFluentMethods(typeof(WhenTrueOverloads), Priority = -1)]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, string> trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, string> falseBecause)
{
    /// <summary>
    /// Creates a specification with descriptive assertions, but using the supplied proposition to succinctly explain
    /// the decision.
    /// </summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public PolicyBase<IEnumerable<TModel>, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromBooleanResultProposition<TModel, string, TMetadata>(
            spec.IsSatisfiedBy,
            higherOrderOperation.HigherOrderPredicate,
            trueBecause,
            falseBecause,
            new SpecDescription(statement, spec.Description),
            higherOrderOperation.CauseSelector);
    }
}
