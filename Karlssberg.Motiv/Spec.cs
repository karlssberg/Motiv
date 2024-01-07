using Karlssberg.Motiv.Builder;
using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv;

/// <summary>Represents a specification that defines a condition for a model and associated metadata.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class Spec<TModel, TMetadata> : SpecificationBase<TModel, TMetadata>
{
    private readonly SpecificationBase<TModel, TMetadata> _specification;

    /// <summary>Gets the description of the specification.</summary>
    public override string Description => _specification.Description;

    /// <summary>Determines whether the specified model satisfies the specification.</summary>
    /// <param name="model">The model to be checked.</param>
    /// <returns>A Boolean result indicating whether the model satisfies the specification.</returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) => _specification.IsSatisfiedBy(model);
    protected static IRequireTrueReason<TAltModel> Build<TAltModel>(Func<TAltModel, bool> predicate) => Spec.Build(predicate);
    
    internal Spec(
        string description,
        Func<TModel, bool> predicate,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        _specification = new GenericMetadataSpecification<TModel, TMetadata>(
            description,
            predicate,
            whenTrue,
            whenFalse);
    }

    protected Spec(SpecificationBase<TModel, TMetadata> specification)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = specification;
    }


    protected Spec(Func<SpecificationBase<TModel, TMetadata>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = specification;
    }
}

/// <summary>Represents a specification that defines a condition for a model of type <typeparamref name="TModel" />.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public class Spec<TModel> : SpecificationBase<TModel, string>
{
    private readonly SpecificationBase<TModel, string> _specification;
    public override string Description => _specification.Description;
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) => _specification.IsSatisfiedBy(model);

    internal Spec(
        string description,
        Func<TModel, bool> predicate,
        Func<TModel, string> whenTrue,
        Func<TModel, string> whenFalse)
    {
        _specification = new StringMetadataSpecification<TModel>(
            description,
            predicate,
            whenTrue,
            whenFalse);
    }

    protected Spec(SpecificationBase<TModel, string> specification)
    {
        _specification = specification.ThrowIfNull(nameof(specification));
    }

    protected Spec(Func<SpecificationBase<TModel, string>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        _specification = specificationFactory()
            .ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
    }
}

public static class Spec
{
    public static IRequireTrueReasonOrMetadata<TModel> Build<TModel>(Func<TModel, bool> predicate) => new SpecBuilder<TModel>(predicate);
    
    public static IRequireTrueMetadata<TModel> Build<TModel, TMetadata>(SpecificationBase<TModel, TMetadata> spec) => new ChangeMetadataBuilder<TModel, TMetadata>(spec);
}