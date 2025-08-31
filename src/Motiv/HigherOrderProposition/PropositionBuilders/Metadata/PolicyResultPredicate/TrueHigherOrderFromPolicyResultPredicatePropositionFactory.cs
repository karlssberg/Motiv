using Motiv.Generator;
using Motiv.HigherOrderProposition.PolicyResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate;

/// <summary>
/// A builder for creating propositions based on a predicate and explanations for true and false conditions.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <param name="resultResolver">The predicate to use for the specification.</param>
/// <param name="higherOrderOperation">The higher-order operation to use for the specification.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata>(
    [MultipleFluentMethods(typeof(PolicyResultBuildOverloads))]Func<TModel, PolicyResultBase<TMetadata>> resultResolver,
    [MultipleFluentMethods(typeof(HigherOrderPredicatePolicyMethods))]HigherOrderPolicyPredicateOperation<TModel, TMetadata> higherOrderOperation)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<IEnumerable<TModel>, TMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromPolicyResultMultiMetadataProposition<TModel, TMetadata, TMetadata>(
            resultResolver,
            higherOrderOperation.HigherOrderPredicate,
            eval => eval.Values,
            eval => eval.Values,
            new SpecDescription(statement),
            higherOrderOperation.CauseSelector);
    }
}
