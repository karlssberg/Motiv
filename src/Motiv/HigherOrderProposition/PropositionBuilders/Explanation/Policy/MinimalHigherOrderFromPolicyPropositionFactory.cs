using System.Linq.Expressions;
using Converj.Attributes;
using Motiv.HigherOrderProposition.PolicyResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories.
/// </summary>
/// <param name="policy">The policy to use for the specification.</param>
/// <param name="higherOrderOperation">The higher-order predicate operation to use for the specification.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
public readonly partial struct MinimalHigherOrderFromPolicyPropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(PolicyBuildOverloads))]PolicyBase<TModel, TMetadata> policy,
    [MultipleFluentMethods(typeof(HigherOrderPredicatePolicyMethods))]HigherOrderPolicyPredicateOperation<TModel, TMetadata> higherOrderOperation)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>
    public SpecBase<IEnumerable<TModel>, TMetadata> Create(string statement) =>
        new MinimalHigherOrderFromPolicyResultProposition<TModel, TMetadata>(
            policy.EvaluatePolicyInternal,
            higherOrderOperation.HigherOrderPredicate,
            new SpecDescription(
                statement.ThrowIfNullOrWhitespace(nameof(statement)),
                policy.Description),
            higherOrderOperation.CauseSelector,
            higherOrderOperation.ShortCircuit);

    internal SpecBase<IEnumerable<TModel>, TMetadata> Create(Expression statement) =>
        new MinimalHigherOrderFromPolicyResultProposition<TModel, TMetadata>(
            policy.EvaluatePolicyInternal,
            higherOrderOperation.HigherOrderPredicate,
            new ExpressionDescription(
                statement,
                policy.Description),
            higherOrderOperation.CauseSelector,
            higherOrderOperation.ShortCircuit);
}
