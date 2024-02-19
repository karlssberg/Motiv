using Karlssberg.Motiv.Propositions;
using Karlssberg.Motiv.Propositions.CompositeFactorySpecBuilders;
using Karlssberg.Motiv.Propositions.FirstOrderSpecBuilders;

namespace Karlssberg.Motiv;

/// <summary>Represents a specification that yields custom metadata based on the outcome of the underlying spec/predicate.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class Spec<TModel, TMetadata> : SpecBase<TModel, TMetadata>
{
    // The base specification associated with the Spec instance.
    private readonly Func<TModel, SpecBase<TModel, TMetadata>> _specFactory;

    // Initializes a new instance of the Spec class with a description, predicate, and functions for when the predicate is true or false.
    internal Spec(
        string description,
        Func<TModel, bool> predicate,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        Description = description.ThrowIfNullOrWhitespace(nameof(description));
        _specFactory = _ => new MetadataPropositionalSpec<TModel, TMetadata>(
            description,
            predicate,
            whenTrue,
            whenFalse);
    }

    // Initializes a new instance of the Spec class with a specification factory.
    internal Spec(string description, Func<TModel, SpecBase<TModel, TMetadata>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        Description = description.ThrowIfNullOrWhitespace(nameof(description));
        _specFactory = specFactory;
    }

    // Initializes a new instance of the Spec class with a SpecBase instance.
    protected Spec(SpecBase<TModel, TMetadata> spec)
    {
        Description = spec.Description;
        spec.ThrowIfNull(nameof(spec));
        _specFactory = _ => spec;
    }

    // Initializes a new instance of the Spec class with a specification factory.
    protected Spec(Func<SpecBase<TModel, TMetadata>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var spec = specificationFactory();
        spec.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        Description = spec.Description;
        _specFactory = _ => spec;
    }

    // Gets the description of the specification.
    public override string Description { get; }

    // Determines whether the specified model satisfies the specification.
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

    // Initializes a new instance of the Spec class with a description, predicate, and functions for when the predicate is true or false.
    internal Spec(
        string description,
        Func<TModel, bool> predicate,
        Func<TModel, string> whenTrue,
        Func<TModel, string> whenFalse)
    {
        Description = description.ThrowIfNullOrWhitespace(nameof(description));
        _specFactory = _ => new PropositionalSpec<TModel>(
            description,
            predicate,
            whenTrue,
            whenFalse);
    }

    // Initializes a new instance of the Spec class with a specification factory.
    internal Spec(string description, Func<TModel, SpecBase<TModel, string>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        Description = description.ThrowIfNullOrWhitespace(nameof(description));
        _specFactory = specFactory;
    }

    // Initializes a new instance of the Spec class with a SpecBase instance.
    protected Spec(SpecBase<TModel, string> spec)
    {
        Description = spec.Description;
        spec.ThrowIfNull(nameof(spec));
        _specFactory = _ => spec;
    }

    // Initializes a new instance of the Spec class with a specification factory.
    protected Spec(Func<SpecBase<TModel, string>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        var spec = specFactory().ThrowIfFactoryOutputIsNull(nameof(specFactory));
        Description = spec.Description;
        _specFactory = _ => spec;
    }

    // Gets the description of the specification.
    public override string Description { get; }

    // Determines whether the specified model satisfies the specification.
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) => _specFactory(model).IsSatisfiedBy(model);
}

public static class Spec
{
    // Builds a specification with a predicate function.
    public static TrueFirstOrderSpecBuilder<TModel> Build<TModel>(Func<TModel, bool> predicate)
    {
        predicate.ThrowIfNull(nameof(predicate));
        return new TrueFirstOrderSpecBuilder<TModel>(predicate);
    }
    public static TrueFirstOrderSpecBuilder<TModel> Build<TModel>(Func<bool> predicate)
    {
        predicate.ThrowIfNull(nameof(predicate));
        return new TrueFirstOrderSpecBuilder<TModel>(_ => predicate());
    }
    
    public static TrueCompositeFactorySpecBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        Func<TModel, SpecBase<TModel, TMetadata>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        return new TrueCompositeFactorySpecBuilder<TModel, TMetadata>(specFactory);
    }
    
    public static TrueCompositeFactorySpecBuilder<TModel, string> Build<TModel>(
        Func<TModel, SpecBase<TModel, string>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        return new TrueCompositeFactorySpecBuilder<TModel, string>(specFactory);
    }
    
    public static TrueCompositeFactorySpecBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        Func<SpecBase<TModel, TMetadata>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        return new TrueCompositeFactorySpecBuilder<TModel, TMetadata>(_ => specFactory());
    }
    
    public static TrueCompositeFactorySpecBuilder<TModel, string> Build<TModel>(
        Func<SpecBase<TModel, string>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        return new TrueCompositeFactorySpecBuilder<TModel, string>(_ => specFactory());
    }
    
    public static SpecBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        SpecBase<TModel, TMetadata> spec)
    {
        spec.ThrowIfNull(nameof(spec));
        return new SpecBuilder<TModel, TMetadata>(spec);
    }
    
    public static SpecBuilder<TModel, string> Build<TModel>(
        SpecBase<TModel, string> spec)
    {
        spec.ThrowIfNull(nameof(spec));
        return new SpecBuilder<TModel, string>(spec);
    }
}

