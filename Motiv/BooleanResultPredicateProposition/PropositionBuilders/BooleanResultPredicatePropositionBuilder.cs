using Motiv.BooleanResultPredicateProposition.PropositionBuilders.Explanation;
using Motiv.BooleanResultPredicateProposition.PropositionBuilders.Metadata;
using Motiv.HigherOrderProposition;
using Motiv.HigherOrderProposition.PropositionBuilders;

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders;

/// <summary>
/// A builder for creating propositions using a predicate function that returns a
/// <see cref="BooleanResultBase{TMetadata}" />.
/// </summary>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the metadata associated with the underlying boolean result.</typeparam>
public readonly ref struct BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate)
{
    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">
    /// A human-readable reason why the condition is true.  This will also be the name of the
    /// proposition, unless otherwise specified by the subsequent <c>Create(string statement)</c> method.
    /// </param>
    /// <returns>An instance of <see cref="FalseAssertionExplanationPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionWithNamePropositionBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        string trueBecause) =>
        new(predicate, trueBecause);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionExplanationPropositionBuilder{TModel, TUnderlyingMetadata}" />.</returns>
    public FalseAssertionExplanationPropositionBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<TModel, string> trueBecause)
    {
        trueBecause.ThrowIfNull(nameof(trueBecause));
        return new FalseAssertionExplanationPropositionBuilder<TModel, TUnderlyingMetadata>(
            predicate,
            (model, _) => trueBecause(model).ToEnumerable());
    }

    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata>(
            predicate,
            (_, _) => whenTrue.ToEnumerable());
    }

    /// <summary>Specifies a metadata factory function to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, TMetadata> whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata>(
            predicate,
            (model, _) => whenTrue(model).ToEnumerable());
    }

    /// <summary>Specifies a metadata factory function to use when the condition is true.</summary>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, string, TUnderlyingMetadata> WhenTrue(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataPropositionBuilder<TModel, string, TUnderlyingMetadata>(
            predicate,
            whenTrue.ToEnumerableReturn());
    }

    /// <summary>Specifies a metadata factory function to use when the condition is true.</summary>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, string, TUnderlyingMetadata> WhenTrueYield(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> whenTrue) =>
        new(predicate, whenTrue.ThrowIfNull(nameof(whenTrue)));

    /// <summary>Specifies a metadata factory function to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    /// <remarks>
    /// <para>
    /// The compiler might not always infer the correct usage of <see cref="TMetadata" /> when collections are used as
    /// a return type.  If so, you will need to be either explicit with your generic arguments, or ensure the return type is explicitly an <c>IEnumerable&lt;T&gt;</c>.
    ///</para>
    /// <para>
    /// For example:
    /// </para>
    /// <para>
    /// <c>.WhenTrue&lt;char&gt;((_, _) =&gt; "hello world");  // ['h', 'e', 'l', 'l', 'o',...]</c>
    /// </para>
    /// <para>
    /// or
    /// </para>
    /// <para>
    /// <c>.WhenTrue((_, _) =&gt; "hello world".AsEnumerable());  // ['h', 'e', 'l', 'l', 'o',...]</c>
    /// </para>
    /// </remarks>
    public FalseMetadataPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata>(
            predicate,
            whenTrue.ToEnumerableReturn());
    }

    /// <summary>Specifies a metadata factory function to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrueYield<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
        new(predicate, whenTrue.ThrowIfNull(nameof(whenTrue)));

    /// <summary>Specifies a higher order predicate for the proposition.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromSpecPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> As(
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate) =>
        new(predicate,
            higherOrderPredicate,
            (isSatisfied, results) => Causes.Get(isSatisfied, results, higherOrderPredicate));

    /// <summary>Specifies a higher order predicate for the proposition.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <param name="causeSelector">A function that selects the causes of the boolean results.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromSpecPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> As(
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>,
            IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector) =>
        new(predicate, higherOrderPredicate, causeSelector);

    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, TUnderlyingMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new BooleanResultPredicateMetadataProposition<TModel, TUnderlyingMetadata, TUnderlyingMetadata>(
            predicate,
            (_, result) => result.Metadata,
            (_, result) => result.Metadata,
            new SpecDescription(statement));
    }
}