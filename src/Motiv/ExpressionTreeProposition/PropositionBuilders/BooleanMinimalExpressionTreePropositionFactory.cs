using System.Linq.Expressions;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// Creates a minimal proposition from a boolean predicate expression tree. The resulting proposition
/// is expression-backed, so the predicate expression can be recovered for use with query providers.
/// </summary>
/// <param name="expression">The expression tree predicate to represent</param>
/// <typeparam name="TModel">The model type</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly partial struct BooleanMinimalExpressionTreePropositionFactory<TModel>(
    [FluentMethod("From")]Expression<Func<TModel, bool>> expression)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An expression-backed proposition for the model.</returns>
    public ExpressionSpecBase<TModel, string> Create(string statement) =>
        new ExpressionSpecDecorator<TModel, string>(
            new MinimalExpressionTreeProposition<TModel, bool>(
                expression,
                (_, result) => result.Values,
                (_, result) => result.Values,
                new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))) { HasExplicitStatement = true }),
            expression);
}
