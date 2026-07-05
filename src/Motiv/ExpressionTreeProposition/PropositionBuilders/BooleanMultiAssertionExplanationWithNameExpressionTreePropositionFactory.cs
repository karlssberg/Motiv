using System.Linq.Expressions;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// </summary>
/// <param name="expression">The expression to use for the specification.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct BooleanMultiAssertionExplanationWithNameExpressionTreePropositionFactory<TModel>(
    [FluentMethod("From")]Expression<Func<TModel, bool>> expression,
    [FluentMethod("WhenTrue")]string trueBecause,
    [FluentMethod("WhenFalseYield")]Func<TModel, BooleanResultBase<string>, IEnumerable<string>> falseBecause)
{
    private Func<TModel, BooleanResultBase<string>, IEnumerable<string>> TrueBecauseFunc =>
        trueBecause
            .ToEnumerable()
            .ToFunc<TModel, BooleanResultBase<string>, IEnumerable<string>>();

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable. Because a name is supplied, the <c>WhenTrue</c>/<c>WhenFalse</c> values are surfaced via <see cref="BooleanResultBase{TMetadata}.Values"/>, not <see cref="BooleanResultBase.Assertions"/>.</remarks>
    /// <returns>An expression-backed proposition for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>
    public ExpressionSpecBase<TModel, string> Create(string statement) =>
        new ExpressionSpecDecorator<TModel, string>(
            new ExpressionTreeMultiMetadataProposition<TModel, string, bool>(
                expression,
                TrueBecauseFunc,
                falseBecause,
                new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)))),
            expression);

    /// <summary>
    /// Creates a proposition with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>An expression-backed proposition for the model.</returns>
    /// <exception cref="ArgumentException">Thrown when the WhenTrue assertion is null, empty or whitespace (it doubles as the propositional statement).</exception>
    public ExpressionSpecBase<TModel, string> Create() =>
        new ExpressionSpecDecorator<TModel, string>(
            new ExpressionTreeMultiAssertionExplanationProposition<TModel, bool>(
                expression,
                TrueBecauseFunc,
                falseBecause,
                new SpecDescription(trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause)))),
            expression);
}
