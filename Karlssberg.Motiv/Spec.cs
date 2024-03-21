using Karlssberg.Motiv.BooleanResultPredicate.BooleanResultPredicateBuilders;
using Karlssberg.Motiv.Composite.CompositeSpecBuilders;
using Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders;
using Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders;

namespace Karlssberg.Motiv;

/// <summary>
/// Represents a specification that yields custom metadata based on the outcome of the underlying spec/predicate.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class Spec<TModel, TMetadata> : SpecBase<TModel, TMetadata>
{
    // The base specification associated with the Spec instance.
    private readonly Func<TModel, SpecBase<TModel, TMetadata>> _specFactory;

    /// <summary>
    /// Initializes a new instance of the Spec class with a SpecBase instance.
    /// </summary>
    /// <param name="spec">The base specification associated with the Spec instance.</param>
    protected Spec(SpecBase<TModel, TMetadata> spec)
    {
        spec.ThrowIfNull(nameof(spec));
        Proposition = spec.Proposition;
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

        Proposition = spec.Proposition;
        _specFactory = _ => spec;
    }

    /// <summary>
    /// Gets the description of the specification.
    /// </summary>
    public override IProposition Proposition { get; }

    /// <summary>
    /// Determines whether the specified model satisfies the specification.
    /// </summary>
    /// <param name="model">The model to be checked against the specification.</param>
    /// <returns>A BooleanResultBase containing the result of the specification check and the associated metadata.</returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) =>
        _specFactory(model).IsSatisfiedBy(model);
}

/// <summary>
/// Represents a specification that defines a condition for a model of type TModel. This specification is
/// associated with a string metadata.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public class Spec<TModel> : SpecBase<TModel, string>
{
    // The base specification associated with the Spec instance.
    private readonly Func<TModel, SpecBase<TModel, string>> _specFactory;

    /// <summary>
    /// Initializes a new instance of the Spec class with a SpecBase instance.
    /// </summary>
    /// <param name="spec">The base specification associated with the Spec instance.</param>
    protected Spec(SpecBase<TModel, string> spec)
    {
        Proposition = spec.Proposition;
        spec.ThrowIfNull(nameof(spec));
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
        Proposition = spec.Proposition;
        _specFactory = _ => spec;
    }

    /// <summary>
    /// Gets the description of the specification.
    /// </summary>
    public override IProposition Proposition { get; }

    /// <summary>
    /// Determines whether the specified model satisfies the specification.
    /// </summary>
    /// <param name="model">The model to be checked against the specification.</param>
    /// <returns>A BooleanResultBase containing the result of the specification check and the associated metadata.</returns>
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) => _specFactory(model).IsSatisfiedBy(model);
}

/// <summary>
/// Creates specifications using a fluent API.
/// </summary>
public static class Spec
{
    /// <summary>
    /// Commences the construction of a specification using a predicate function.
    /// </summary>
    /// <param name="predicate">The predicate function to be used in the specification.</param>
    /// <returns>A TrueFirstOrderSpecBuilder instance for further specification building.</returns>
    public static TrueFirstOrderSpecBuilder<TModel> Build<TModel>(Func<TModel, bool> predicate)
    {
        predicate.ThrowIfNull(nameof(predicate));
        return new TrueFirstOrderSpecBuilder<TModel>(predicate);
    }
    
    /// <summary>
    /// Commences the construction of a specification using a predicate function that returns a <see cref="BooleanResultBase{TMetadata}"/>.
    /// </summary>
    /// <param name="predicate">The predicate function to be used in the specification.</param>
    /// <returns>A TrueFirstOrderSpecBuilder instance for further specification building.</returns>
    public static TrueBooleanResultPredicateSpecBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        Func<TModel, BooleanResultBase<TMetadata>> predicate)
    {
        predicate.ThrowIfNull(nameof(predicate));
        return new TrueBooleanResultPredicateSpecBuilder<TModel, TMetadata>(predicate);
    }
    
    /// <summary>
    /// Commences the construction of a specification using a specification factory function.
    /// </summary>
    /// <param name="specFactory">The specification factory function to be used in the specification.</param>
    /// <returns>A TrueCompositeFactorySpecBuilder instance for further specification building.</returns>
    public static TrueCompositeFactorySpecBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        Func<TModel, SpecBase<TModel, TMetadata>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        return new TrueCompositeFactorySpecBuilder<TModel, TMetadata>(specFactory);
    }

    /// <summary>
    /// Commences the construction of a specification using a specification factory function.
    /// </summary>
    /// <param name="specFactory">The specification factory function to be used in the specification.</param>
    /// <returns>A TrueCompositeFactorySpecBuilder instance for further specification building.</returns>
    public static TrueCompositeFactorySpecBuilder<TModel, string> Build<TModel>(
        Func<TModel, SpecBase<TModel, string>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        return new TrueCompositeFactorySpecBuilder<TModel, string>(specFactory);
    }

    /// <summary>
    /// Commences the construction of a specification using a specification factory function.
    /// </summary>
    /// <param name="specFactory">The specification factory function to be used in the specification.</param>
    /// <returns>A TrueCompositeFactorySpecBuilder instance for further specification building.</returns>
    public static TrueCompositeFactorySpecBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        Func<SpecBase<TModel, TMetadata>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        return new TrueCompositeFactorySpecBuilder<TModel, TMetadata>(_ => specFactory());
    }

    /// <summary>
    /// Commences the construction of a specification using a specification factory function.
    /// </summary>
    /// <param name="specFactory">The specification factory function to be used in the specification.</param>
    /// <returns>A TrueCompositeFactorySpecBuilder instance for further specification building.</returns>
    public static TrueCompositeFactorySpecBuilder<TModel, string> Build<TModel>(
        Func<SpecBase<TModel, string>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        return new TrueCompositeFactorySpecBuilder<TModel, string>(_ => specFactory());
    }

    /// <summary>
    /// Commences the construction of a specificaton that is derived from an existing specifcation.
    /// </summary>
    /// <param name="spec">The SpecBase instance to be used in the specification.</param>
    /// <returns>A TrueCompositeSpecBuilder instance for further specification building.</returns>
    public static TrueCompositeSpecBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        SpecBase<TModel, TMetadata> spec)
    {
        spec.ThrowIfNull(nameof(spec));
        return new TrueCompositeSpecBuilder<TModel, TMetadata>(spec);
    }
}