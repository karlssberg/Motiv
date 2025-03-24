using System.Linq.Expressions;
using Motiv.Generator.Attributes;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Spec;

/// <summary>
/// A factory for creating propositions based on a predicate and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct MinimalHigherOrderFromSpecPropositionFactory<TModel>(
    [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, string> spec,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string statement) =>
        new MinimalHigherOrderFromBooleanResultProposition<TModel, string>(
            spec.IsSatisfiedBy,
            higherOrderOperation.HigherOrderPredicate,
            new SpecDescription(
                statement.ThrowIfNullOrWhitespace(nameof(statement)),
                spec.Description),
            higherOrderOperation.CauseSelector);

    internal SpecBase<IEnumerable<TModel>, string> Create(Expression statement) =>
        new MinimalHigherOrderFromBooleanResultProposition<TModel, string>(
            spec.IsSatisfiedBy,
            higherOrderOperation.HigherOrderPredicate,
            new ExpressionDescription(
                statement,
                spec.Description),
            higherOrderOperation.CauseSelector);
}

