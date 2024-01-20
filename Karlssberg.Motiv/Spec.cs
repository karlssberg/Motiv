using Karlssberg.Motiv.ChangeMetadata;
using Karlssberg.Motiv.SpecBuilder;
using Karlssberg.Motiv.SpecBuilder.Phase1;

namespace Karlssberg.Motiv;

/// <summary>Represents a specification that defines a condition for a model and associated metadata.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class Spec<TModel, TMetadata> : SpecBase<TModel, TMetadata>
{
    private readonly SpecBase<TModel, TMetadata> _spec;

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

    protected Spec(SpecBase<TModel, TMetadata> spec)
    {
        spec.ThrowIfNull(nameof(spec));
        _spec = spec;
    }


    protected Spec(Func<SpecBase<TModel, TMetadata>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _spec = specification;
    }

    /// <summary>Gets the description of the specification.</summary>
    public override string Description => _spec.Description;

    /// <summary>Determines whether the specified model satisfies the specification.</summary>
    /// <param name="model">The model to be checked.</param>
    /// <returns>A Boolean result indicating whether the model satisfies the specification.</returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) => _spec.IsSatisfiedBy(model);
    protected static IRequireTrueReason<TAltModel> Build<TAltModel>(Func<TAltModel, bool> predicate) => Spec.Build(predicate);
}

/// <summary>Represents a specification that defines a condition for a model of type <typeparamref name="TModel" />.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public class Spec<TModel> : SpecBase<TModel, string>
{
    private readonly SpecBase<TModel, string> _spec;

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

    protected Spec(SpecBase<TModel, string> spec)
    {
        _spec = spec.ThrowIfNull(nameof(spec));
    }

    protected Spec(Func<SpecBase<TModel, string>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        _spec = specificationFactory()
            .ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
    }
    public override string Description => _spec.Description;
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) => _spec.IsSatisfiedBy(model);
}

public static class Spec
{
    public static IRequireTrueReasonOrMetadata<TModel> Build<TModel>(Func<TModel, bool> predicate) => new SpecBuilder<TModel>(predicate);

    public static IRequireTrueMetadata<TModel> Build<TModel, TMetadata>(SpecBase<TModel, TMetadata> spec) => new ChangeMetadataBuilder<TModel, TMetadata>(spec);
}