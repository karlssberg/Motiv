using Motiv.FluentFactory.Attributes;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Spec;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public readonly struct MultiAssertionFromSpecHigherOrderPropositionFactory<TModel, TMetadata>
{
    private readonly SpecBase<TModel, TMetadata> _spec;
    private readonly HigherOrderSpecPredicateOperation<TModel, TMetadata> _higherOrderOperation;
    private readonly Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<string>> _whenTrue;
    private readonly Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<string>> _whenFalse;

    /// <summary>
    /// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
    /// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
    /// every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    /// <param name="spec">The specification to decorate.</param>
    /// <param name="higherOrderOperation">The higher-order predicate operation.</param>
    /// <param name="whenTrue">The explanation for when the predicate is true.</param>
    /// <param name="whenFalse">The explanation for when the predicate is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), CreateMethod = CreateMethod.None)]
    public MultiAssertionFromSpecHigherOrderPropositionFactory(
        [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, TMetadata> spec,
        [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<string>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<string>> whenFalse)
    {
        _spec = spec;
        _higherOrderOperation = higherOrderOperation;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

    /// <summary>
    /// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
    /// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
    /// every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    /// <param name="spec">The specification to decorate.</param>
    /// <param name="higherOrderOperation">The higher-order predicate operation.</param>
    /// <param name="whenTrue">The explanation for when the predicate is true.</param>
    /// <param name="whenFalse">The explanation for when the predicate is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), CreateMethod = CreateMethod.None)]
    public MultiAssertionFromSpecHigherOrderPropositionFactory(
        [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, TMetadata> spec,
        [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation,
        [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, string> whenTrue,
        [FluentMethod("WhenFalseYield", Priority = -1)]Func<HigherOrderBooleanResultEvaluation<TModel, TMetadata>, IEnumerable<string>> whenFalse)
    {
        _spec = spec;
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
        return new HigherOrderFromBooleanResultMultiMetadataProposition<TModel, string, TMetadata>(
            _spec.Evaluate,
            _higherOrderOperation.HigherOrderPredicate,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement, _spec.Description) { HasExplicitStatement = true },
            _higherOrderOperation.CauseSelector);
    }
}
