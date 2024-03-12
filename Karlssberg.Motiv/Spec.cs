﻿using Karlssberg.Motiv.Composite.CompositeSpecBuilders;
using Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders;
using Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders;

namespace Karlssberg.Motiv;

/// <summary>Represents a specification that yields custom metadata based on the outcome of the underlying spec/predicate.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class Spec<TModel, TMetadata> : SpecBase<TModel, TMetadata>
{
    // The base specification associated with the Spec instance.
    private readonly Func<TModel, SpecBase<TModel, TMetadata>> _specFactory;
    
    // Initializes a new instance of the Spec class with a SpecBase instance.
    protected Spec(SpecBase<TModel, TMetadata> spec)
    {
        spec.ThrowIfNull(nameof(spec));
        Proposition = spec.Proposition;
        _specFactory = _ => spec;
    }

    // Initializes a new instance of the Spec class with a specification factory.
    protected Spec(Func<SpecBase<TModel, TMetadata>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var spec = specificationFactory();
        spec.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        
        Proposition = spec.Proposition;
        _specFactory = _ => spec;
    }

    // Gets the description of the specification.
    public override IProposition Proposition { get; }

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

    // Initializes a new instance of the Spec class with a SpecBase instance.
    protected Spec(SpecBase<TModel, string> spec)
    {
        Proposition = spec.Proposition;
        spec.ThrowIfNull(nameof(spec));
        _specFactory = _ => spec;
    }

    // Initializes a new instance of the Spec class with a specification factory.
    protected Spec(Func<SpecBase<TModel, string>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));
        var spec = specFactory().ThrowIfFactoryOutputIsNull(nameof(specFactory));
        Proposition = spec.Proposition;
        _specFactory = _ => spec;
    }

    // Gets the description of the specification.
    public override IProposition Proposition { get; }

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
    
    public static TrueCompositeSpecBuilder<TModel, TMetadata> Build<TModel, TMetadata>(
        SpecBase<TModel, TMetadata> spec)
    {
        spec.ThrowIfNull(nameof(spec));
        return new TrueCompositeSpecBuilder<TModel, TMetadata>(spec);
    }
}

