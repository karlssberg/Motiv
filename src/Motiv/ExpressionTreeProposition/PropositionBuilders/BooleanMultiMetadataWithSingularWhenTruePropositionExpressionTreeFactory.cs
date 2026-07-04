using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <param name="expression">The expression to use for the specification.</param>
/// <param name="whenTrue">The metadata factory for the proposition when the predicate is true.</param>
/// <param name="whenFalse">The metadata factory for the proposition when the predicate is false.</param>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct BooleanMultiMetadataWithSingularWhenTruePropositionExpressionTreeFactory<TModel, TMetadata>(
    [FluentMethod("From")]Expression<Func<TModel, bool>> expression,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<string>, TMetadata> whenTrue,
    [FluentMethod("WhenFalseYield")]Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> whenFalse)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An expression-backed proposition for the model.</returns>
    public ExpressionSpecBase<TModel, TMetadata> Create(string statement) =>
        new ExpressionSpecDecorator<TModel, TMetadata>(
            new ExpressionTreeMultiMetadataProposition<TModel, TMetadata, bool>(
                expression,
                whenTrue.ToEnumerableReturn(),
                whenFalse,
                new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))) { HasExplicitStatement = true }),
            expression);
}
