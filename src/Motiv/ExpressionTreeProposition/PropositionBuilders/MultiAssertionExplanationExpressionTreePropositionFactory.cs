using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;
using Motiv.Generator.Attributes;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create
/// a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TPredicateResult">The return type of the predicate expression.</typeparam>
public readonly partial struct MultiAssertionExplanationExpressionTreePropositionFactory<TModel, TPredicateResult>
{
    private readonly Expression<Func<TModel, TPredicateResult>> _expression;
    private readonly Func<TModel, BooleanResultBase<string>, IEnumerable<string>> _trueBecause;
    private readonly Func<TModel, BooleanResultBase<string>, IEnumerable<string>> _falseBecause;

    /// <summary>
    /// A factory for creating propositions based on the supplied proposition and explanation factories.
    /// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create
    /// a proposition that covers every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiAssertionExplanationExpressionTreePropositionFactory(
        [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, BooleanResultBase<string>, IEnumerable<string>> trueBecause,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, BooleanResultBase<string>, IEnumerable<string>> falseBecause)
    {
        _expression = expression;
        _trueBecause = trueBecause;
        _falseBecause = falseBecause;
    }

    /// <summary>
    /// A factory for creating propositions based on the supplied proposition and explanation factories.
    /// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create
    /// a proposition that covers every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiAssertionExplanationExpressionTreePropositionFactory(
        [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
        [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<string>, string> trueBecause,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, BooleanResultBase<string>, IEnumerable<string>> falseBecause)
    {
        _expression = expression;
        _trueBecause = trueBecause.ToEnumerableReturn();
        _falseBecause = falseBecause;
    }

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string statement) =>
        new ExpressionTreeMultiMetadataProposition<TModel, string, TPredicateResult>(
            _expression,
            _trueBecause,
            _falseBecause,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))));
}
