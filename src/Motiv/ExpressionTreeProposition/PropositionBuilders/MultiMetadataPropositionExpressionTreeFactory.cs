using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;
using Motiv.Generator.Attributes;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TPredicateResult">The return type of the predicate expression.</typeparam>
public readonly partial struct MultiMetadataPropositionExpressionTreeFactory<TModel, TMetadata, TPredicateResult>
{
    private readonly Expression<Func<TModel, TPredicateResult>> _expression;
    private readonly Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> _whenTrue;
    private readonly Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> _whenFalse;

    /// <summary>
    /// A factory for creating propositions based on the supplied proposition and metadata factories.
    /// </summary>
    [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiMetadataPropositionExpressionTreeFactory(
        [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> whenTrue,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> whenFalse)
    {
        _expression = expression;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

    /// <summary>
    /// A factory for creating propositions based on the supplied proposition and metadata factories.
    /// </summary>
    [FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiMetadataPropositionExpressionTreeFactory(
        [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
        [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<string>, TMetadata> whenTrue,
        [FluentMethod("WhenFalseYield")]Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> whenFalse)
    {
        _expression = expression;
        _whenTrue = whenTrue.ToEnumerableReturn();
        _whenFalse = whenFalse;
    }

    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, TMetadata> Create(string statement) =>
        new ExpressionTreeMultiMetadataProposition<TModel, TMetadata, TPredicateResult>(
            _expression,
            _whenTrue,
            _whenFalse,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))));
}
