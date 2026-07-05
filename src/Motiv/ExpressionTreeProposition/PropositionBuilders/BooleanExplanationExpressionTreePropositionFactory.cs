using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// A factory for creating expression-backed propositions based on the supplied proposition and
/// explanation factories.
/// </summary>
/// <param name="expression">The expression to use for the specification.</param>
/// <param name="trueBecause">The explanation to use when the expression evaluates to true.</param>
/// <param name="falseBecause">The explanation to use when the expression evaluates to false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct BooleanExplanationExpressionTreePropositionFactory<TModel>(
    [FluentMethod("From")]Expression<Func<TModel, bool>> expression,
    [MultipleFluentMethods(typeof(WhenTrueLambdaOverloads))]Func<TModel, BooleanResultBase<string>, string> trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, BooleanResultBase<string>, string> falseBecause)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An expression-backed policy for the model.</returns>
    public ExpressionPolicyBase<TModel, string> Create(string statement) =>
        new ExpressionPolicyDecorator<TModel, string>(
            new ExpressionTreeMetadataProposition<TModel, string, bool>(
                expression,
                trueBecause,
                falseBecause,
                new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))) { HasExplicitStatement = true }),
            expression);

    /// <summary>
    /// Creates a proposition. The propositional statement is derived from the expression itself.
    /// </summary>
    /// <returns>An expression-backed policy for the model.</returns>
    public ExpressionPolicyBase<TModel, string> Create() =>
        new ExpressionPolicyDecorator<TModel, string>(
            new ExpressionTreeExplanationProposition<TModel, bool>(
                expression,
                trueBecause,
                falseBecause,
                new ExpressionTreeDescription<TModel, bool>(expression)),
            expression);
}
