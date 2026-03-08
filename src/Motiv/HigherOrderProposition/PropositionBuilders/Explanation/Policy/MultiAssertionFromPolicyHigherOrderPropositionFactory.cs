using Motiv.FluentFactory.Attributes;
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
public readonly struct MultiAssertionFromPolicyHigherOrderPropositionFactory<TModel, TMetadata>
{
    private readonly PolicyBase<TModel, TMetadata> _policy;
    private readonly HigherOrderPolicyPredicateOperation<TModel, TMetadata> _higherOrderOperation;
    private readonly Func<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, IEnumerable<string>> _whenTrue;
    private readonly Func<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, IEnumerable<string>> _whenFalse;

    /// <summary>
    /// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
    /// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
    /// every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    /// <param name="policy">The policy to decorate.</param>
    /// <param name="higherOrderOperation">The higher-order predicate operation.</param>
    /// <param name="whenTrue">The explanation for when the policy is true.</param>
    /// <param name="whenFalse">The explanation for when the policy is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiAssertionFromPolicyHigherOrderPropositionFactory(
        [MultipleFluentMethods(typeof(PolicyBuildOverloads))]PolicyBase<TModel, TMetadata> policy,
        [MultipleFluentMethods(typeof(HigherOrderPredicatePolicyMethods))]HigherOrderPolicyPredicateOperation<TModel, TMetadata> higherOrderOperation,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, IEnumerable<string>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, IEnumerable<string>> whenFalse)
    {
        _policy = policy;
        _higherOrderOperation = higherOrderOperation;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

    /// <summary>
    /// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
    /// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
    /// every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    /// <param name="policy">The policy to decorate.</param>
    /// <param name="higherOrderOperation">The higher-order predicate operation.</param>
    /// <param name="whenTrue">The explanation for when the policy is true.</param>
    /// <param name="whenFalse">The explanation for when the policy is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiAssertionFromPolicyHigherOrderPropositionFactory(
        [MultipleFluentMethods(typeof(PolicyBuildOverloads))]PolicyBase<TModel, TMetadata> policy,
        [MultipleFluentMethods(typeof(HigherOrderPredicatePolicyMethods))]HigherOrderPolicyPredicateOperation<TModel, TMetadata> higherOrderOperation,
        [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, string> whenTrue,
        [FluentMethod("WhenFalseYield", Priority = -1)]Func<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, IEnumerable<string>> whenFalse)
    {
        _policy = policy;
        _higherOrderOperation = higherOrderOperation;
        _whenTrue = whenTrue.ToEnumerableReturn();
        _whenFalse = whenFalse;
    }

    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromPolicyResultMultiMetadataProposition<TModel, string, TMetadata>(
            _policy.Evaluate,
            _higherOrderOperation.HigherOrderPredicate,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement, _policy.Description) { HasExplicitStatement = true },
            _higherOrderOperation.CauseSelector);
    }
}
