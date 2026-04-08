using Converj.Attributes;
using Motiv.HigherOrderProposition.PolicyResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.PolicyResultPredicate;

/// <summary>
/// A factory for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the specification.</typeparam>
/// <param name="resultResolver">The function to resolve the policy result.</param>
/// <param name="higherOrderOperation">The higher-order predicate operation.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
[FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct MultiAssertionExplanationFromPolicyResultHigherOrderWithSingularWhenTruePropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(PolicyResultBuildOverloads))]Func<TModel, PolicyResultBase<TMetadata>> resultResolver,
    [MultipleFluentMethods(typeof(HigherOrderPredicatePolicyMethods))]HigherOrderPolicyPredicateOperation<TModel, TMetadata> higherOrderOperation,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, string> trueBecause,
    [FluentMethod("WhenFalseYield")]Func<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, IEnumerable<string>> falseBecause)
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
        return new HigherOrderFromPolicyResultMultiMetadataProposition<TModel, string, TMetadata>(
            resultResolver,
            higherOrderOperation.HigherOrderPredicate,
            trueBecause.ToEnumerableReturn(),
            falseBecause,
            new SpecDescription(statement) { HasExplicitStatement = true },
            higherOrderOperation.CauseSelector);
    }
}
