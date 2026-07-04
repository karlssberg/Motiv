using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create
/// a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <param name="expression">The expression to use for the specification.</param>
/// <param name="trueBecause">The explanation to use when the expression evaluates to true.</param>
/// <param name="falseBecause">The explanation to use when the expression evaluates to false.</param>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct BooleanMultiAssertionExplanationWithSingularWhenTrueExpressionTreePropositionFactory<TModel>(
    [FluentMethod("From")]Expression<Func<TModel, bool>> expression,
    [MultipleFluentMethods(typeof(WhenTrueLambdaOverloads))]Func<TModel, BooleanResultBase<string>, string> trueBecause,
    [FluentMethod("WhenFalseYield")]Func<TModel, BooleanResultBase<string>, IEnumerable<string>> falseBecause)
{
    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An expression-backed proposition for the model.</returns>
    public ExpressionSpecBase<TModel, string> Create(string statement) =>
        new ExpressionSpecDecorator<TModel, string>(
            new ExpressionTreeMultiMetadataProposition<TModel, string, bool>(
                expression,
                trueBecause.ToEnumerableReturn(),
                falseBecause,
                new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))) { HasExplicitStatement = true }),
            expression);
}
