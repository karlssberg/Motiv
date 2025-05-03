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
[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct  MetadataExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult>(
    [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<string>, TMetadata> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, BooleanResultBase<string>, TMetadata> whenFalse)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, TMetadata> Create(string statement) =>
        new ExpressionTreeMetadataProposition<TModel, TMetadata, TPredicateResult>(
            expression,
            whenTrue,
            whenFalse,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))));
}
