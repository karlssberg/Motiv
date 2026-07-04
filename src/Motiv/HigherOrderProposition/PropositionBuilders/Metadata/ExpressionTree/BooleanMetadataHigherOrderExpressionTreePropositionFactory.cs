using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Converj.Attributes;
using Motiv.HigherOrderProposition.ExpressionTree;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree;

/// <summary>
/// A factory for creating propositions based on a boolean predicate expression and metadata factories. This is
/// particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <param name="expression">The expression to use for the specification.</param>
/// <param name="higherOrderOperation">The higher-order operation to use for the specification.</param>
/// <param name="whenTrue">The metadata factory for when the predicate is true.</param>
/// <param name="whenFalse">The metadata factory for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
[FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct BooleanMetadataHigherOrderExpressionTreePropositionFactory<TModel, TMetadata>(
    [FluentMethod("From")]Expression<Func<TModel, bool>> expression,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> whenFalse)
{
    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public PolicyBase<IEnumerable<TModel>, TMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromExpressionTreeMetadataProposition<TModel, TMetadata, bool>(
            expression,
            higherOrderOperation.HigherOrderPredicate,
            whenTrue,
            whenFalse,
            new SpecDescription(statement) { HasExplicitStatement = true },
            higherOrderOperation.CauseSelector);
    }

    /// <summary>Creates a specification.</summary>
    /// <returns>A specification for the model.</returns>
    public PolicyBase<IEnumerable<TModel>, TMetadata> Create()
    {
        return new HigherOrderFromExpressionTreeMetadataProposition<TModel, TMetadata, bool>(
            expression,
            higherOrderOperation.HigherOrderPredicate,
            whenTrue,
            whenFalse,
            new ExpressionTreeDescription<TModel, bool>(expression),
            higherOrderOperation.CauseSelector);
    }
}
