using Motiv.BooleanPredicateProposition.PropositionBuilders;
using Motiv.BooleanResultPredicateProposition.PropositionBuilders;
using Motiv.SpecDecoratorProposition.PropositionBuilders;

namespace Motiv;

/// <summary>
/// Represents a proposition that yields custom metadata based on the outcome of the underlying spec/predicate.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class Spec<TModel, TMetadata> : SpecBase<TModel, TMetadata>
{
    private readonly SpecBase<TModel, TMetadata>? _spec;

    // The base proposition associated with the Spec instance.
    private readonly Func<TModel, SpecBase<TModel, TMetadata>> _specFactory;

    /// <summary>
    /// Initializes a new instance of the Spec class with a SpecBase instance.
    /// </summary>
    /// <param name="spec">The base proposition associated with the Spec instance.</param>
    protected Spec(SpecBase<TModel, TMetadata> spec)
    {
        spec.ThrowIfNull(nameof(spec));
        
        Description = spec.Description;
        _spec = spec;
        _specFactory = _ => spec;
    }

    /// <summary>
    /// Initializes a new instance of the Spec class with a specification factory.
    /// </summary>
    /// <param name="specificationFactory">The specification factory to create the Spec instance.</param>
    protected Spec(Func<SpecBase<TModel, TMetadata>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var spec = specificationFactory();
        spec.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));

        Description = spec.Description;
        _specFactory = _ => spec;
    }
    
    /// <summary>
    /// Gets the underlying specifications that make up this composite proposition.
    /// </summary>
    public override IEnumerable<SpecBase> Underlying => _spec is null
        ? Enumerable.Empty<SpecBase<TModel>>()
        : _spec.Underlying;

    /// <summary>
    /// Gets the description of the proposition.
    /// </summary>
    public override ISpecDescription Description { get; }

    /// <summary>
    /// Determines whether the specified model satisfies the proposition.
    /// </summary>
    /// <param name="model">The model to be checked against the proposition.</param>
    /// <returns>A BooleanResultBase containing the result of the proposition check and the associated metadata.</returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) =>
        _specFactory(model).IsSatisfiedBy(model);
}

/// <summary>
/// Represents a proposition that defines a condition for a model of type TModel. This proposition is
/// associated with a string metadata.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public class Spec<TModel> : SpecBase<TModel, string>
{
    private readonly SpecBase<TModel, string>? _spec;

    // The base proposition associated with the Spec instance.
    private readonly Func<TModel, SpecBase<TModel, string>> _specFactory;

    /// <summary>
    /// Initializes a new instance of the Spec class with a SpecBase instance.
    /// </summary>
    /// <param name="spec">The base proposition associated with the Spec instance.</param>
    protected Spec(SpecBase<TModel, string> spec)
    {
        spec.ThrowIfNull(nameof(spec));
        
        Description = spec.Description;
        _spec = spec;
        _specFactory = _ => spec;
    }

    /// <summary>
    /// Initializes a new instance of the Spec class with a specification factory.
    /// </summary>
    /// <param name="specFactory">The specification factory to create the Spec instance.</param>
    protected Spec(Func<SpecBase<TModel, string>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        var spec = specFactory().ThrowIfFactoryOutputIsNull(nameof(specFactory));
        Description = spec.Description;
        _specFactory = _ => spec;
    }

    /// <summary>
    /// Gets the description of the proposition.
    /// </summary>
    public override ISpecDescription Description { get; }

    /// <summary>
    /// Gets the underlying specifications that makes up this composite proposition.
    /// </summary>
    public override IEnumerable<SpecBase> Underlying => Enumerable.Empty<SpecBase<TModel>>();

    /// <summary>
    /// Determines whether the specified model satisfies the proposition.
    /// </summary>
    /// <param name="model">The model to be checked against the proposition.</param>
    /// <returns>
    /// A BooleanResultBase containing the result of the proposition being applied to a moel and the associated metadata.
    /// </returns>
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) => _specFactory(model).IsSatisfiedBy(model);
}

/// <summary>
/// Creates propositions using a fluent API.
/// </summary>
public static class Spec
{
    /// <summary>
    /// Commences the construction of a proposition using a predicate function.
    /// </summary>
    /// <param name="predicate">The predicate function to be used in the proposition.</param>
    /// <returns>A TrueFirstOrderSpecBuilder instance for further proposition building.</returns>
    public static BooleanPredicatePropositionBuilder<TModel> Build<TModel>(Func<TModel, bool> predicate)
    {
        predicate.ThrowIfNull(nameof(predicate));
        return new BooleanPredicatePropositionBuilder<TModel>(predicate);
    }
    
    /// <summary>
    /// Commences the construction of a proposition using a predicate function that returns a <see cref="BooleanResultBase{TMetadata}"/>.
    /// </summary>
    /// <param name="predicate">The predicate function to be used in the proposition.</param>
    /// <returns>A TrueFirstOrderSpecBuilder instance for further proposition building.</returns>
    public static BooleanResultPredicatePropositionBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        Func<TModel, BooleanResultBase<TMetadata>> predicate)
    {
        predicate.ThrowIfNull(nameof(predicate));
        return new BooleanResultPredicatePropositionBuilder<TModel, TMetadata>(predicate);
    }

    /// <summary>
    /// Commences the construction of a specification using a specification factory function.
    /// </summary>
    /// <param name="factory">The specification factory function to be used in the specification.</param>
    /// <returns>A TrueCompositeFactorySpecBuilder instance for further specification building.</returns>
    public static TruePropositionBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        Func<SpecBase<TModel, TMetadata>> factory)
    {
        factory.ThrowIfNull(nameof(factory));
        return new TruePropositionBuilder<TModel, TMetadata>(factory());
    }

    /// <summary>
    /// Commences the construction of a specification using a specification factory function.
    /// </summary>
    /// <param name="factory">The specification factory function to be used in the specification.</param>
    /// <returns>A TrueCompositeFactorySpecBuilder instance for further specification building.</returns>
    public static TruePropositionBuilder<TModel, string> Build<TModel>(
        Func<SpecBase<TModel, string>> factory)
    {
        factory.ThrowIfNull(nameof(factory));
        return new TruePropositionBuilder<TModel, string>(factory());
    }

    /// <summary>
    /// Commences the construction of a specificaton that is derived from an existing specifcation.
    /// </summary>
    /// <param name="proposition">The proposition upon which to derive a new proposition.</param>
    /// <returns>A TrueCompositeSpecBuilder instance for further specification building.</returns>
    public static TruePropositionBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        SpecBase<TModel, TMetadata> proposition)
    {
        proposition.ThrowIfNull(nameof(proposition));
        return new TruePropositionBuilder<TModel, TMetadata>(proposition);
    }
}