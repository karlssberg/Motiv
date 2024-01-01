using Karlssberg.Motive.ChangeMetadataType;

namespace Karlssberg.Motive;

public class Spec<TModel, TMetadata> : SpecificationBase<TModel, TMetadata>
{
    private readonly SpecificationBase<TModel, TMetadata> _specification;
    public override string Description => _specification.Description;
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) => _specification.IsSatisfiedBy(model);

    #region PredicateConstructors

    public Spec(
        string description,
        Func<TModel, bool> predicate,
        TMetadata whenTrue,
        TMetadata whenFalse)
    {
        _specification = new GenericMetadataSpecification<TModel, TMetadata>(
            description,
            predicate,
            whenTrue,
            whenFalse);
    }

    public Spec(
        string description,
        Func<TModel, bool> predicate,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse)
    {
        _specification = new GenericMetadataSpecification<TModel, TMetadata>(
            description,
            predicate,
            whenTrue,
            whenFalse);
    }

    public Spec(
        string description,
        Func<TModel, bool> predicate,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        _specification = new GenericMetadataSpecification<TModel, TMetadata>(
            description,
            predicate,
            whenTrue,
            whenFalse);
    }

    #endregion

    #region SpecificationConstructors

    public Spec(
        SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenTrue,
        TMetadata whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }

    public Spec(
        SpecificationBase<TModel, TMetadata> specification,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }

    public Spec(
        SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }

    public Spec(SpecificationBase<TModel, TMetadata> specification)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = specification;
    }

    #endregion

    #region SpecificationConstructorsWithDescription

    public Spec(
        string description,
        SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenTrue,
        TMetadata whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = specification.SubstituteMetadata(description, whenTrue, whenFalse);
    }

    public Spec(
        string description,
        SpecificationBase<TModel, TMetadata> specification,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = specification.SubstituteMetadata(description, whenTrue, whenFalse);
    }

    public Spec(
        string description,
        SpecificationBase<TModel, TMetadata> specification,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = specification.SubstituteMetadata(description, whenTrue, whenFalse);
    }

    public Spec(
        string description,
        SpecificationBase<TModel, TMetadata> specification)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = new ChangeDescriptionSpecification<TModel, TMetadata>(description, specification);
    }

    #endregion

    #region SpecificationFactoryConstructors

    public Spec(
        Func<SpecificationBase<TModel, TMetadata>> specificationFactory,
        TMetadata whenTrue,
        TMetadata whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }

    public Spec(
        Func<SpecificationBase<TModel, TMetadata>> specificationFactory,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }

    public Spec(
        Func<SpecificationBase<TModel, TMetadata>> specificationFactory,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }

    public Spec(Func<SpecificationBase<TModel, TMetadata>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = specification;
    }

    #endregion

    #region SpecificationFactoryConstructorsDescription

    public Spec(
        string description,
        Func<SpecificationBase<TModel, TMetadata>> specificationFactory,
        TMetadata whenTrue,
        TMetadata whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = specification.SubstituteMetadata(description, whenTrue, whenFalse);
    }

    public Spec(
        string description,
        Func<SpecificationBase<TModel, TMetadata>> specificationFactory,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = specification.SubstituteMetadata(description, whenTrue, whenFalse);
    }

    public Spec(
        string description,
        Func<SpecificationBase<TModel, TMetadata>> specificationFactory,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = specification.SubstituteMetadata(description, whenTrue, whenFalse);
    }

    public Spec(
        string description,
        Func<SpecificationBase<TModel, TMetadata>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = new ChangeDescriptionSpecification<TModel, TMetadata>(description, specification);
    }

    #endregion


    #region StringMetadataSpecificationConstructors

    public Spec(
        SpecificationBase<TModel, string> specification,
        TMetadata whenTrue,
        TMetadata whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(specification, _ => whenTrue, _ => whenFalse);
    }

    public Spec(
        SpecificationBase<TModel, string> specification,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(specification, whenTrue, _ => whenFalse);
    }

    public Spec(
        SpecificationBase<TModel, string> specification,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(specification, _ => whenTrue, whenFalse);
    }

    public Spec(
        SpecificationBase<TModel, string> specification,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(specification, whenTrue, whenFalse);
    }

    #endregion

    #region StringMetadataSpecificationConstructorsWithDescription

    public Spec(
        string description,
        SpecificationBase<TModel, string> specification,
        TMetadata whenTrue,
        TMetadata whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(
            description,
            specification,
            _ => whenTrue,
            _ => whenFalse);
    }

    public Spec(
        string description,
        SpecificationBase<TModel, string> specification,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(
            description,
            specification,
            whenTrue,
            _ => whenFalse);
    }

    public Spec(
        string description,
        SpecificationBase<TModel, string> specification,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(
            description,
            specification,
            _ => whenTrue,
            whenFalse);
    }


    public Spec(
        string description,
        SpecificationBase<TModel, string> specification,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        specification.ThrowIfNull(nameof(specification));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(
            description,
            specification,
            whenTrue,
            whenFalse);
    }

    #endregion

    #region StringMetadataSpecificationFactoryConstructors

    public Spec(
        Func<SpecificationBase<TModel, string>> specificationFactory,
        TMetadata whenTrue,
        TMetadata whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(specification, _ => whenTrue, _ => whenFalse);
    }

    public Spec(
        Func<SpecificationBase<TModel, string>> specificationFactory,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(specification, whenTrue, _ => whenFalse);
    }

    public Spec(
        Func<SpecificationBase<TModel, string>> specificationFactory,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(specification, _ => whenTrue, whenFalse);
    }

    #endregion

    #region StringMetadataSpecificationFactoryConstructorsWithDescription

    public Spec(
        string description,
        Func<SpecificationBase<TModel, string>> specificationFactory,
        TMetadata whenTrue,
        TMetadata whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(
            description,
            specification,
            _ => whenTrue,
            _ => whenFalse);
    }

    public Spec(
        string description,
        Func<SpecificationBase<TModel, string>> specificationFactory,
        Func<TModel, TMetadata> whenTrue,
        TMetadata whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(
            description,
            specification,
            whenTrue,
            _ => whenFalse);
    }

    public Spec(
        string description,
        Func<SpecificationBase<TModel, string>> specificationFactory,
        TMetadata whenTrue,
        Func<TModel, TMetadata> whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        var specification = specificationFactory();
        specification.ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(
            description,
            specification,
            _ => whenTrue,
            whenFalse);
    }

    #endregion
}

public class Spec<TModel> : SpecificationBase<TModel, string>
{
    private readonly SpecificationBase<TModel, string> _specification;
    public override string Description => _specification.Description;
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model) => _specification.IsSatisfiedBy(model);

    #region PredicateConstructors

    public Spec(
        Func<TModel, bool> predicate,
        string whenTrue,
        string whenFalse)
    {
        _specification = new StringMetadataSpecification<TModel>(
            predicate,
            whenTrue,
            whenFalse);
    }

    public Spec(
        string description,
        Func<TModel, bool> predicate,
        Func<TModel, string> whenTrue,
        string whenFalse)
    {
        _specification = new StringMetadataSpecification<TModel>(
            description,
            predicate,
            whenTrue,
            whenFalse);
    }

    public Spec(
        Func<TModel, bool> predicate,
        string whenTrue,
        Func<TModel, string> whenFalse)
    {
        _specification = new StringMetadataSpecification<TModel>(
            predicate,
            whenTrue,
            whenFalse);
    }

    public Spec(
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

    #endregion

    #region SpecificationConstructors

    public Spec(
        SpecificationBase<TModel, string> specification,
        string whenTrue,
        string whenFalse)
    {
        _specification = specification
            .ThrowIfNull(nameof(specification))
            .SubstituteMetadata(whenTrue, whenFalse);
    }

    public Spec(
        SpecificationBase<TModel, string> specification,
        Func<TModel, string> whenTrue,
        string whenFalse)
    {
        _specification = specification
            .ThrowIfNull(nameof(specification))
            .SubstituteMetadata(whenTrue, whenFalse);
    }

    public Spec(
        SpecificationBase<TModel, string> specification,
        string whenTrue,
        Func<TModel, string> whenFalse)
    {
        _specification = specification
            .ThrowIfNull(nameof(specification))
            .SubstituteMetadata(whenTrue, whenFalse);
    }

    public Spec(SpecificationBase<TModel, string> specification)
    {
        _specification = specification.ThrowIfNull(nameof(specification));
    }

    #endregion

    #region SpecificationFactoryConstructors

    public Spec(
        Func<SpecificationBase<TModel, string>> specificationFactory,
        string whenTrue,
        string whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        _specification = specificationFactory()
            .ThrowIfFactoryOutputIsNull(nameof(specificationFactory))
            .SubstituteMetadata(whenTrue, whenFalse);
    }

    public Spec(
        Func<SpecificationBase<TModel, string>> specificationFactory,
        Func<TModel, string> whenTrue,
        string whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        _specification = specificationFactory()
            .ThrowIfFactoryOutputIsNull(nameof(specificationFactory))
            .SubstituteMetadata(whenTrue, whenFalse);
    }

    public Spec(
        Func<SpecificationBase<TModel, string>> specificationFactory,
        string whenTrue,
        Func<TModel, string> whenFalse)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        _specification = specificationFactory()
            .ThrowIfFactoryOutputIsNull(nameof(specificationFactory))
            .SubstituteMetadata(whenTrue, whenFalse);
    }

    public Spec(Func<SpecificationBase<TModel, string>> specificationFactory)
    {
        specificationFactory.ThrowIfNull(nameof(specificationFactory));
        _specification = specificationFactory()
            .ThrowIfFactoryOutputIsNull(nameof(specificationFactory));
    }

    #endregion
}