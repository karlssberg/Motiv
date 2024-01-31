using Karlssberg.Motiv.SpecBuilder;
using Karlssberg.Motiv.SpecBuilder.YieldWhenTrue;

namespace Karlssberg.Motiv;

/// <summary>
/// Represents a specification that yields custom metadata based on the outcome of the underlying spec/predicate.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class Spec<TModel, TMetadata> : SpecBase<TModel, TMetadata>
{
    // The base specification associated with the Spec instance.
    private readonly SpecBase<TModel, TMetadata> _spec;

    // Initializes a new instance of the Spec class with a description, predicate, and functions for when the predicate is true or false.
    internal Spec(
        string description,
        Func<TModel, bool> predicate,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        _spec = new GenericMetadataSpec<TModel, TMetadata>(
            description,
            predicate,
            whenTrue,
            whenFalse);
    }

    // Initializes a new instance of the Spec class with a SpecBase instance.
    protected Spec(SpecBase<TModel, TMetadata> spec)
    {
        spec.ThrowIfNull(nameof(spec));
        _spec = spec;
    }

    // Initializes a new instance of the Spec class with a specification factory.
    protected Spec(Func<SpecBase<TModel, TMetadata>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _spec = specification;
    }

    // Gets the description of the specification.
    public override string Description => _spec.Description;

    // Determines whether the specified model satisfies the specification.
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) => _spec.IsSatisfiedBy(model);

    // Builds a specification with a predicate function.
    protected static IYieldReasonWhenTrue<TAltModel> Build<TAltModel>(Func<TAltModel, bool> predicate) =>
        Spec.Build(predicate);
}

/// <summary>
/// Represents a specification that defines a condition for a model of type TModel.
/// This specification is associated with a string metadata.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public class Spec<TModel> : SpecBase<TModel, string>
{
    // The base specification associated with the Spec instance.
    private readonly SpecBase<TModel, string> _spec;

    // Initializes a new instance of the Spec class with a description, predicate, and functions for when the predicate is true or false.
    internal Spec(
        string description,
        Func<TModel, bool> predicate,
        Func<TModel, string> whenTrue,
        Func<TModel, string> whenFalse)
    {
        _spec = new StringMetadataSpec<TModel>(
            description,
            predicate,
            whenTrue,
            whenFalse);
    }

    // Initializes a new instance of the Spec class with a SpecBase instance.
    protected Spec(SpecBase<TModel, string> spec)
    {
        _spec = spec.ThrowIfNull(nameof(spec));
    }

    // Initializes a new instance of the Spec class with a specification factory.
    protected Spec(Func<SpecBase<TModel, string>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        _spec = specificationFactory()
            .ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
    }

    // Gets the description of the specification.
    public override string Description => _spec.Description;

    // Determines whether the specified model satisfies the specification.
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) => _spec.IsSatisfiedBy(model);
}

public static class Spec
{
    // Builds a specification with a predicate function.
    public static IYieldReasonOrMetadataWhenTrue<TModel> Build<TModel>(Func<TModel, bool> predicate) =>
        new ReasonSpecBuilder<TModel>(predicate);
}