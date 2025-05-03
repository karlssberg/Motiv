using Motiv.DecoratorProposition.PropositionBuilders.Overloads;
using Motiv.Generator.Attributes;
using Motiv.Shared;

namespace Motiv.DecoratorProposition.PropositionBuilders.Spec;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and metadata factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TReplacementMetadata">The type of the metadata associated with the proposition.</typeparam>
/// <typeparam name="TMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct PropositionFactory<TModel, TReplacementMetadata, TMetadata>(
    [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, TMetadata> spec,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<TMetadata>, TReplacementMetadata> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, BooleanResultBase<TMetadata>, TReplacementMetadata> whenFalse)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, TReplacementMetadata> Create(string statement) =>
        new SpecDecoratorProposition<TModel, TReplacementMetadata, TMetadata>(
            spec,
            whenTrue,
            whenFalse,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)), spec.Description));
}

[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct PropositionFactory<TModel, TReplacementMetadata>(
    [MultipleFluentMethods(typeof(SpecBuildOverloads))]SpecBase<TModel, string> spec,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<string>, TReplacementMetadata> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, BooleanResultBase<string>, TReplacementMetadata> whenFalse)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, TReplacementMetadata> Create(string statement) =>
        new SpecDecoratorProposition<TModel, TReplacementMetadata, string>(
            spec,
            whenTrue,
            whenFalse,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)), spec.Description));
}
