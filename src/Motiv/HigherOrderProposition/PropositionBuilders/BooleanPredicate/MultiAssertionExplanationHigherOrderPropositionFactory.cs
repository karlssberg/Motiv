using Motiv.ExpressionTreeProposition;
using Motiv.Generator.Attributes;
using Motiv.HigherOrderProposition.BooleanPredicate;

namespace Motiv.HigherOrderProposition.PropositionBuilders.BooleanPredicate;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
/// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
/// every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly partial struct MultiAssertionExplanationHigherOrderPropositionFactory<TModel>
{
    private readonly Func<TModel, bool> _resultResolver;
    private readonly HigherOrderSpecBooleanPredicateOperation<TModel> _higherOrderOperation;
    private readonly Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> _whenTrue;
    private readonly Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> _whenFalse;

    /// <summary>
    /// A factory for creating propositions based on a predicate and metadata factories. This is particularly useful
    /// for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers
    /// every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    [FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiAssertionExplanationHigherOrderPropositionFactory(
        [FluentMethod("Build")]Func<TModel, bool> resultResolver,
        [MultipleFluentMethods(typeof(HigherOrderBooleanPredicateSpecMethods))]HigherOrderSpecBooleanPredicateOperation<TModel> higherOrderOperation,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> whenFalse)
    {
        resultResolver.ThrowIfNull(nameof(resultResolver));
        _resultResolver = resultResolver;
        _higherOrderOperation = higherOrderOperation;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, string>  Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromBooleanPredicateMultiMetadataProposition<TModel,string>(
            _resultResolver,
            _higherOrderOperation.HigherOrderPredicate,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement),
            _higherOrderOperation.CauseSelector);
    }
}

