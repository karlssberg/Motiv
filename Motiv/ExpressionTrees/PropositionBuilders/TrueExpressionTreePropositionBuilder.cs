using System.Linq.Expressions;
using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.PropositionBuilders;
using Motiv.ExpressionTrees.PropositionBuilders.Metadata;
using Motiv.ExpressionTrees.PropositionBuilders.Explanation;
using Motiv.Shared;

namespace Motiv.ExpressionTrees.PropositionBuilders;

/// <summary>
/// A builder for creating propositions based on a lambda expression tree.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly ref struct TrueExpressionTreePropositionBuilder<TModel>(
    Expression<Func<TModel, bool>> expression)
{
    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, TMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(expression, (_, _) => whenTrue);

    /// <summary>Specifies an assertion to yield when the proposition is true.</summary>
    /// <param name="whenTrue">Metadata to yield when the proposition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionWithNamePropositionBuilder{TModel}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, TMetadata> WhenTrue<TMetadata>(
        Func<TModel, TMetadata> whenTrue) =>
        new(expression, (model, _) => whenTrue(model));

    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates the metadata when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata}" />.</returns>
    /// <remarks>
    /// <para>
    /// If you wish to return a collection of metadata items, you will need to use the <c>WhenTrueYield()</c>
    /// method instead, otherwise the whole collection will become the metadata.
    /// </para>
    /// </remarks>
    public FalseMetadataPropositionBuilder<TModel, TMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<string>, TMetadata> whenTrue) =>
        new(expression, whenTrue);

    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a collection of metadata when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata}" />.</returns>
    public FalseMultiMetadataPropositionBuilder<TModel, TMetadata> WhenTrueYield<TMetadata>(
        Func<TModel, BooleanResultBase<string>, IEnumerable<TMetadata>> whenTrue) =>
        new(expression, whenTrue);

    /// <summary>Specifies an assertion to yield when the condition is true.  This will also be the name of the proposition, unless otherwise
    /// specified by the subsequent <c>Create(string statement)</c> method.</summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true. </param>
    /// <returns>An instance of <see cref="FalseAssertionWithNamePropositionBuilder{TModel}" />.</returns>
    public FalseAssertionWithNamePropositionBuilder<TModel> WhenTrue(
        string trueBecause) =>
        new(expression, trueBecause);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionPropositionBuilder{TModel}" />.</returns>
    public FalseAssertionPropositionBuilder<TModel> WhenTrue(
        Func<TModel, string> trueBecause) =>
        new(expression, (model, _) => trueBecause(model));

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionPropositionBuilder{TModel}" />.</returns>
    public FalseAssertionPropositionBuilder<TModel> WhenTrue(
        Func<TModel, BooleanResultBase<string>, string> trueBecause) =>
        new(expression, trueBecause);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMultiMetadataPropositionBuilder{TModel, TMetadata}" />.</returns>
    public FalseMultiMetadataPropositionBuilder<TModel, string> WhenTrueYield(
        Func<TModel, BooleanResultBase<string>, IEnumerable<string>> trueBecause) =>
        new(expression, trueBecause);

    /// <summary>Specifies a higher order predicate for the proposition.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromSpecPropositionBuilder{TModel, TMetadata}" />.</returns>
    public TrueHigherOrderFromSpecPropositionBuilder<TModel, string> As(
        Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate) =>
        new(
            expression.ToSpec(),
            higherOrderPredicate,
            (isSatisfied, results) => Causes.Get(isSatisfied, results, higherOrderPredicate));

    /// <summary>Specifies a higher order predicate for the proposition.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <param name="causeSelector">A function that selects the causes of the boolean results.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromSpecPropositionBuilder{TModel, TMetadata}" />.</returns>
    public TrueHigherOrderFromSpecPropositionBuilder<TModel, string> As(
        Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<BooleanResult<TModel, string>>,
            IEnumerable<BooleanResult<TModel, string>>> causeSelector) =>
        new(expression.ToSpec(), higherOrderPredicate, causeSelector);

    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string statement) =>
        new ExpressionTreeMultiMetadataProposition<TModel, string>(
            expression,
            (_, result) => result.Values,
            (_, result) => result.Values,
            statement.ThrowIfNullOrWhitespace(nameof(statement)));
}
