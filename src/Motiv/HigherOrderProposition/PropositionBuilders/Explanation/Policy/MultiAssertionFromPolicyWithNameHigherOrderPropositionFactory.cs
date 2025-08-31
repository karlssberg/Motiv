using Motiv.FluentFactory.Generator;
using Motiv.HigherOrderProposition.PolicyResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy;

/// <summary>
/// A factory for creating specifications based on a predicate and explanations for true and false conditions.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <param name="policy">The policy to decorate.</param>
/// <param name="higherOrderOperation">The higher-order predicate operation.</param>
/// <param name="trueBecause">The explanation for when the policy is true.</param>
/// <param name="falseBecause">The explanation for when the policy is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the specification.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly struct MultiAssertionFromPolicyWithNameHigherOrderPropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(PolicyBuildOverloads))]PolicyBase<TModel, TMetadata> policy,
    [MultipleFluentMethods(typeof(HigherOrderPredicatePolicyMethods))]HigherOrderPolicyPredicateOperation<TModel, TMetadata> higherOrderOperation,
    [FluentMethod("WhenTrue")]string trueBecause,
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
            policy.IsSatisfiedBy,
            higherOrderOperation.HigherOrderPredicate,
            trueBecause
                .ToEnumerable()
                .ToFunc<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, IEnumerable<string>>(),
            falseBecause,
            new SpecDescription(statement, policy.Description),
            higherOrderOperation.CauseSelector);
    }

    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create() =>
        new HigherOrderFromPolicyResultMultiMetadataProposition<TModel, string, TMetadata>(
            policy.IsSatisfiedBy,
            higherOrderOperation.HigherOrderPredicate,
            trueBecause
                .ToEnumerable()
                .ToFunc<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, IEnumerable<string>>(),
            falseBecause,
            new SpecDescription(trueBecause, policy.Description),
            higherOrderOperation.CauseSelector);
}
