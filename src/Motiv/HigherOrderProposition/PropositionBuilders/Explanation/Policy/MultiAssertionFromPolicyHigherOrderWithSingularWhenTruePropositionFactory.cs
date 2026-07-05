using Converj.Attributes;
using Motiv.HigherOrderProposition.PolicyResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the policy</typeparam>
/// <param name="policy">The policy to decorate.</param>
/// <param name="higherOrderOperation">The higher-order predicate operation.</param>
/// <param name="whenTrue">The explanation for when the policy is true.</param>
/// <param name="whenFalse">The explanation for when the policy is false.</param>
[FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct MultiAssertionFromPolicyHigherOrderWithSingularWhenTruePropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(PolicyBuildOverloads))]PolicyBase<TModel, TMetadata> policy,
    [MultipleFluentMethods(typeof(HigherOrderPredicatePolicyMethods))]HigherOrderPolicyPredicateOperation<TModel, TMetadata> higherOrderOperation,
    [FluentMethod("WhenTrue")]Func<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, string> whenTrue,
    [FluentMethod("WhenFalseYield")]Func<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, IEnumerable<string>> whenFalse)
{
    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromPolicyResultMultiMetadataProposition<TModel, string, TMetadata>(
            policy.Evaluate,
            higherOrderOperation.HigherOrderPredicate,
            whenTrue.ToEnumerableReturn(),
            whenFalse,
            new SpecDescription(statement, policy.Description),
            higherOrderOperation.CauseSelector);
    }
}
