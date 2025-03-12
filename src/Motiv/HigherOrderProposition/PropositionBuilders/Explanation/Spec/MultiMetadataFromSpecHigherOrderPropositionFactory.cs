using Motiv.Generator.Attributes;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Spec;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TReplacementMetadata">The type of the metadata associated with the specification.</typeparam>
public readonly partial struct MultiMetadataFromSpecHigherOrderPropositionFactory<TModel, TReplacementMetadata>
{
    private readonly SpecBase<TModel, string> _spec;
    private readonly HigherOrderSpecPredicateOperation<TModel, string> _higherOrderOperation;
    private readonly Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TReplacementMetadata>> _whenTrue;
    private readonly Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TReplacementMetadata>> _whenFalse;

    /// <summary>
    /// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
    /// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
    /// every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TReplacementMetadata">The type of the metadata associated with the specification.</typeparam>
    [FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiMetadataFromSpecHigherOrderPropositionFactory(
        [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, string> spec,
        [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TReplacementMetadata>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TReplacementMetadata>> whenFalse)
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
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TReplacementMetadata">The type of the metadata associated with the specification.</typeparam>
    [FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiMetadataFromSpecHigherOrderPropositionFactory(
        [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, string> spec,
        [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation,
        [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, TReplacementMetadata> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TReplacementMetadata>> whenFalse)
    {
        _spec = spec;
        _higherOrderOperation = higherOrderOperation;
        _whenTrue = whenTrue.ToEnumerableReturn();
        _whenFalse = whenFalse;
    }

    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, TReplacementMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromBooleanResultMultiMetadataProposition<TModel, TReplacementMetadata, string>(
            _spec.IsSatisfiedBy,
            _higherOrderOperation.HigherOrderPredicate,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement, _spec.Description),
            _higherOrderOperation.CauseSelector);
    }
}
