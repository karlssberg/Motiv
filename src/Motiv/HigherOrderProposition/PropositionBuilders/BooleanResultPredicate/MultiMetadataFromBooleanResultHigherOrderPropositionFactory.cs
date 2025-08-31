using Motiv.FluentFactory.Generator;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.BooleanResultPredicate;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TReplacementMetadata">The type of the metadata associated with the specification.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly struct MultiMetadataFromBooleanResultHigherOrderPropositionFactory<TModel, TReplacementMetadata, TMetadata>
{
    private readonly Func<TModel, BooleanResultBase<TMetadata>> _resultResolver;
    private readonly HigherOrderSpecPredicateOperation<TModel, TMetadata> _higherOrderOperation;
    private readonly Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<TReplacementMetadata>> _whenTrue;
    private readonly Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<TReplacementMetadata>> _whenFalse;

    /// <summary>
    /// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
    /// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
    /// every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    /// <param name="resultResolver">The predicate to use for the specification.</param>
    /// <param name="higherOrderOperation">The higher-order operation to use for the specification.</param>
    /// <param name="whenTrue">The metadata factory for when the predicate is true.</param>
    /// <param name="whenFalse">The metadata factory for when the predicate is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiMetadataFromBooleanResultHigherOrderPropositionFactory(
        [MultipleFluentMethods(typeof(BooleanResultBuildOverloads))]Func<TModel, BooleanResultBase<TMetadata>> resultResolver,
        [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<TReplacementMetadata>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<TReplacementMetadata>> whenFalse)
    {
        _resultResolver = resultResolver;
        _higherOrderOperation = higherOrderOperation;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

    /// <summary>
    /// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
    /// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
    /// every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    /// <param name="resultResolver">The predicate to use for the specification.</param>
    /// <param name="higherOrderOperation">The higher-order operation to use for the specification.</param>
    /// <param name="whenTrue">The metadata factory for when the predicate is true.</param>
    /// <param name="whenFalse">The metadata factory for when the predicate is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiMetadataFromBooleanResultHigherOrderPropositionFactory(
        [MultipleFluentMethods(typeof(BooleanResultBuildOverloads))]Func<TModel, BooleanResultBase<TMetadata>> resultResolver,
        [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation,
        [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, TReplacementMetadata> whenTrue,
        [FluentMethod("WhenFalseYield", Priority = -1)]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<TReplacementMetadata>> whenFalse)
    {
        _resultResolver = resultResolver;
        _higherOrderOperation = higherOrderOperation;
        _whenTrue = whenTrue.ToEnumerableReturn();
        _whenFalse = whenFalse;
    }

    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, TReplacementMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromBooleanResultMultiMetadataProposition<TModel, TReplacementMetadata, TMetadata>(
            _resultResolver,
            _higherOrderOperation.HigherOrderPredicate,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement),
            _higherOrderOperation.CauseSelector);
    }
}
