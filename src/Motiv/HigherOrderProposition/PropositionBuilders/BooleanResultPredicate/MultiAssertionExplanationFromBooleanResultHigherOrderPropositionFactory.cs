using Motiv.FluentFactory.Attributes;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.BooleanResultPredicate;

/// <summary>
/// A factory for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly struct MultiAssertionExplanationFromBooleanResultHigherOrderPropositionFactory<TModel, TMetadata>
{
    private readonly Func<TModel, BooleanResultBase<TMetadata>> _resultResolver;
    private readonly HigherOrderSpecPredicateOperation<TModel, TMetadata> _higherOrderOperation;
    private readonly Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<string>> _trueBecause;
    private readonly Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<string>> _falseBecause;

    /// <summary>
    /// A factory for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    /// <param name="resultResolver">The predicate to use for the specification.</param>
    /// <param name="higherOrderOperation">The higher-order operation to use for the specification.</param>
    /// <param name="trueBecause">The explanation for when the predicate is true.</param>
    /// <param name="falseBecause">The explanation for when the predicate is false.</param>
    [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiAssertionExplanationFromBooleanResultHigherOrderPropositionFactory(
        [MultipleFluentMethods(typeof(BooleanResultBuildOverloads))]Func<TModel, BooleanResultBase<TMetadata>> resultResolver,
        [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<string>> trueBecause,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<string>> falseBecause)
    {
        _resultResolver = resultResolver;
        _higherOrderOperation = higherOrderOperation;
        _trueBecause = trueBecause;
        _falseBecause = falseBecause;
    }

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
            _resultResolver,
            _higherOrderOperation.HigherOrderPredicate,
            _trueBecause,
            _falseBecause,
            new SpecDescription(statement),
            _higherOrderOperation.CauseSelector);
    }
}
