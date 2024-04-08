using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;
using Karlssberg.Motiv.SpecDecorator.SpecDecoratorSpecBuilders.Explanation;
using Karlssberg.Motiv.SpecDecorator.SpecDecoratorSpecBuilders.Metadata;

namespace Karlssberg.Motiv.SpecDecorator.SpecDecoratorSpecBuilders;

/// <summary>
/// A builder for creating propositions based on an existing proposition.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly ref struct TrueSpecDecoratorSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec)
{
    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataSpecDecoratorSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataSpecDecoratorSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(spec, (_, _) => whenTrue.ToEnumerable());

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionWithPropositionSpecDecoratorSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataSpecDecoratorSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, TMetadata> whenTrue) =>
        new(spec, (model, _) => whenTrue(model).ToEnumerable());

    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates the metadata when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataSpecDecoratorSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataSpecDecoratorSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue) =>
        new(spec, (model, result) => whenTrue(model, result).ToEnumerable());

    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a collection of metadata when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataSpecDecoratorSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataSpecDecoratorSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
        new(spec, whenTrue);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionWithPropositionSpecDecoratorSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionWithPropositionSpecDecoratorSpecBuilder<TModel, TUnderlyingMetadata>
        WhenTrue(string trueBecause) =>
        new(spec, (_, _) => trueBecause, trueBecause);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionSpecDecoratorSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionSpecDecoratorSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(Func<TModel, string> trueBecause) =>
        new(spec, (model, _) => trueBecause(model));

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionSpecDecoratorSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionSpecDecoratorSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause) =>
        new(spec, trueBecause);

    /// <summary>Specifies a higher order predicate for the proposition.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromUnderlyingSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public TrueHigherOrderFromUnderlyingSpecBuilder<TModel, TUnderlyingMetadata> As(
        Func<IEnumerable<HigherOrder.BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate) =>
        new(spec, higherOrderPredicate);

    /// <summary>Specifies a higher order predicate for the proposition.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <param name="causeSelector">A function that selects the causes of the boolean results.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromUnderlyingSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public TrueHigherOrderFromUnderlyingSpecBuilder<TModel, TUnderlyingMetadata> As(
        Func<IEnumerable<HigherOrder.BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<HigherOrder.BooleanResult<TModel, TUnderlyingMetadata>>,
            IEnumerable<HigherOrder.BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector) =>
        new(spec, higherOrderPredicate, causeSelector);

    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="proposition">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string proposition) =>
        new SpecDecoratorMetadataSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            (_, _) => proposition,
            (_, _) => $"!{proposition}",
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}