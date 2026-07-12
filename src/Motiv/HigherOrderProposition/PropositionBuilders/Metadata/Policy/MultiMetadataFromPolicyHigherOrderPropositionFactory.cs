using Converj.Attributes;
using Motiv.HigherOrderProposition.PolicyResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.Policy;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TReplacementMetadata">The type of the metadata associated with the specification.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the specification.</typeparam>
/// <param name="policy">The policy to decorate.</param>
/// <param name="higherOrderOperation">The higher-order operation to use for the specification.</param>
/// <param name="whenTrue">The metadata factory for when the predicate is true.</param>
/// <param name="whenFalse">The metadata factory for when the predicate is false.</param>
[FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct MultiMetadataFromPolicyHigherOrderPropositionFactory<TModel, TReplacementMetadata, TMetadata>(
    [MultipleFluentMethods(typeof(PolicyBuildOverloads))]PolicyBase<TModel, TMetadata> policy,
    [MultipleFluentMethods(typeof(HigherOrderPredicatePolicyMethods))]HigherOrderPolicyPredicateOperation<TModel, TMetadata> higherOrderOperation,
    [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, IEnumerable<TReplacementMetadata>> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<HigherOrderPolicyResultEvaluation<TModel, TMetadata>, IEnumerable<TReplacementMetadata>> whenFalse)
{
    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>
    public SpecBase<IEnumerable<TModel>, TReplacementMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromPolicyResultMultiMetadataProposition<TModel, TReplacementMetadata, TMetadata>(
            policy.Evaluate,
            higherOrderOperation.HigherOrderPredicate,
            whenTrue,
            whenFalse,
            new SpecDescription(statement, policy.Description),
            higherOrderOperation.CauseSelector,
            higherOrderOperation.ShortCircuit);
    }
}
