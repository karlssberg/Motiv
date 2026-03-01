using Motiv.FluentFactory.Attributes;

namespace Motiv;

/// <summary>Represents a proposition that yields custom metadata based on the outcome of the underlying spec/predicate.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class Spec<TModel, TMetadata> : SpecBase<TModel, TMetadata>
{
    private readonly SpecBase<TModel, TMetadata> _spec;

    /// <summary>Initializes a new instance of the Spec class with a SpecBase instance.</summary>
    /// <param name="spec">The base proposition associated with the Spec instance.</param>
    protected Spec(SpecBase<TModel, TMetadata> spec)
    {
        spec.ThrowIfNull(nameof(spec));

        _spec = spec;
        Description = spec.Description;
    }

    /// <summary>Initializes a new instance of the Spec class with a specification factory.</summary>
    /// <param name="specificationFactory">The specification factory to create the Spec instance.</param>
    protected Spec(Func<SpecBase<TModel, TMetadata>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));

        _spec = specificationFactory().ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        Description = _spec.Description;
    }

    /// <summary>Gets the underlying specifications that make up this composite proposition.</summary>
    public override IEnumerable<SpecBase> Underlying => _spec.Underlying;

    /// <summary>Gets the description of the proposition.</summary>
    public override ISpecDescription Description { get; }

    /// <inheritdoc />
    public override bool Matches(TModel model) => _spec.Matches(model);

    /// <summary>Determines whether the specified model satisfies the proposition.</summary>
    /// <param name="model">The model to be checked against the proposition.</param>
    /// <returns>A BooleanResultBase containing the result of the proposition check and the associated metadata.</returns>
    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TModel model) =>
        _spec.IsSatisfiedBy(model);
}

/// <summary>
/// Represents a proposition that defines a condition for a model of type TModel. This proposition is associated
/// with a string metadata.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public class Spec<TModel> : SpecBase<TModel, string>
{
    private readonly SpecBase<TModel, string> _spec;

    /// <summary>Initializes a new instance of the Spec class with a SpecBase instance.</summary>
    /// <param name="spec">The base proposition associated with the Spec instance.</param>
    protected Spec(SpecBase<TModel, string> spec)
    {
        spec.ThrowIfNull(nameof(spec));

        _spec = spec;
        Description = spec.Description;
    }

    /// <summary>Initializes a new instance of the Spec class with a specification factory.</summary>
    /// <param name="specFactory">The specification factory to create the Spec instance.</param>
    protected Spec(Func<SpecBase<TModel, string>> specFactory)
    {
        specFactory.ThrowIfNull(nameof(specFactory));

        _spec = specFactory().ThrowIfFactoryOutputIsNull(nameof(specFactory));
        Description = _spec.Description;
    }

    /// <summary>Gets the description of the proposition.</summary>
    public override ISpecDescription Description { get; }

    /// <summary>Gets the underlying specifications that make up this composite proposition.</summary>
    public override IEnumerable<SpecBase> Underlying => _spec.Underlying;

    /// <inheritdoc />
    public override bool Matches(TModel model) => _spec.Matches(model);

    /// <summary>Determines whether the specified model satisfies the proposition.</summary>
    /// <param name="model">The model to be checked against the proposition.</param>
    /// <returns>
    /// A BooleanResultBase containing the result of the proposition being applied to a model and the associated
    /// metadata.
    /// </returns>
    protected override BooleanResultBase<string> IsSpecSatisfiedBy(TModel model) => _spec.IsSatisfiedBy(model);
}

/// <summary>Creates propositions using a fluent API.</summary>
[FluentFactory]
public static partial class Spec;
