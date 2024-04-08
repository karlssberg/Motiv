using Karlssberg.Motiv.SpecFactoryDecorator;
using Karlssberg.Motiv.SpecFactoryDecorator.SpecFactoryDecoratorSpecBuilders.Explanation;
using Karlssberg.Motiv.SpecFactoryDecorator.SpecFactoryDecoratorSpecBuilders.Metadata;

namespace Karlssberg.Motiv;

/// <summary>
/// Represents a builder for creating propositions based on a predicate. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly ref struct SpecFactoryDecoratorSpecBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate)
{
    /// <summary>
    /// Specifies the metadata to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataSpecFactoryDecoratorSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataSpecFactoryDecoratorSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(specPredicate,
            (_, _) => whenTrue.ToEnumerable());

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataSpecFactoryDecoratorSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataSpecFactoryDecoratorSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, TMetadata> whenTrue) =>
        new(specPredicate,
            (model, _) => whenTrue(model).ToEnumerable());

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataSpecFactoryDecoratorSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataSpecFactoryDecoratorSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue) =>
        new(specPredicate,
            (model, result) => whenTrue(model, result).ToEnumerable());

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataSpecFactoryDecoratorSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataSpecFactoryDecoratorSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
        new(specPredicate,
            whenTrue);

    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionWithPropositionSpecFactoryDecoratorSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionWithPropositionSpecFactoryDecoratorSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        string trueBecause) =>
        new(specPredicate,
            (_, _) => trueBecause,
            trueBecause);

    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionSpecFactoryDecoratorSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionSpecFactoryDecoratorSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<TModel, string> trueBecause) =>
        new(specPredicate,
            (model, _) => trueBecause(model));

    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionSpecFactoryDecoratorSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionSpecFactoryDecoratorSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause) =>
        new(specPredicate,
            trueBecause);

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="proposition">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string proposition) =>
        new SpecFactoryDecoratorMetadataSpec<TModel, string, TUnderlyingMetadata>(
            specPredicate,
            (_, _) => proposition,
            (_, _) => $"!{proposition}",
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}