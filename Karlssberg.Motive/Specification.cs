using Karlssberg.Motive.ChangeMetadataType;

namespace Karlssberg.Motive;

public class Specification<TModel, TMetadata> : SpecificationBase<TModel, TMetadata>
{
    private readonly SpecificationBase<TModel, TMetadata> _specification;

    #region PredicateConstructors
    public Specification(
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
    
    public Specification(
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
    
    public Specification(
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
    public Specification(
        SpecificationBase<TModel, TMetadata> specification, 
        TMetadata whenTrue, 
        TMetadata whenFalse)
    {
        Throw.IfNull(specification, nameof(specification));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }
    
    public Specification(
        SpecificationBase<TModel, TMetadata> specification, 
        Func<TModel, TMetadata> whenTrue, 
        TMetadata whenFalse)
    {
        Throw.IfNull(specification, nameof(specification));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }
    
    public Specification(
        SpecificationBase<TModel, TMetadata> specification, 
        TMetadata whenTrue, 
        Func<TModel, TMetadata> whenFalse)
    {
        Throw.IfNull(specification, nameof(specification));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }
    public Specification(SpecificationBase<TModel, TMetadata> specification)
    {
        Throw.IfNull(specification, nameof(specification));
        _specification = specification;
    }
    #endregion
    #region SpecificationFactoryConstructors
    public Specification(
        Func<SpecificationBase<TModel, TMetadata>> specificationFactory, 
        TMetadata whenTrue, 
        TMetadata whenFalse)
    {
        Throw.IfNull(specificationFactory, nameof(specificationFactory));
        var specification = specificationFactory();
        Throw.IfFactoryOutputIsNull(specification, nameof(specificationFactory));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }
    
    public Specification(
        Func<SpecificationBase<TModel, TMetadata>> specificationFactory, 
        Func<TModel, TMetadata> whenTrue, 
        TMetadata whenFalse)
    {
        Throw.IfNull(specificationFactory, nameof(specificationFactory));
        var specification = specificationFactory();
        Throw.IfFactoryOutputIsNull(specification, nameof(specificationFactory));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }
    
    public Specification(
        Func<SpecificationBase<TModel, TMetadata>> specificationFactory, 
        TMetadata whenTrue, 
        Func<TModel, TMetadata> whenFalse)
    {
        Throw.IfNull(specificationFactory, nameof(specificationFactory));
        var specification = specificationFactory();
        Throw.IfFactoryOutputIsNull(specification, nameof(specificationFactory));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }
    
    public Specification(Func<SpecificationBase<TModel, TMetadata>> specificationFactory)
    {
        Throw.IfNull(specificationFactory, nameof(specificationFactory));
        var specification = specificationFactory();
        Throw.IfFactoryOutputIsNull(specification, nameof(specificationFactory));
        _specification = specification;
    }
    #endregion
    #region BasicSpecificationConstructors
    public Specification(
        SpecificationBase<TModel, string> specification, 
        TMetadata whenTrue, 
        TMetadata whenFalse)
    {
        Throw.IfNull(specification, nameof(specification));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(specification, _ => whenTrue, _ => whenFalse);
    }
    
    public Specification(
        SpecificationBase<TModel, string> specification, 
        Func<TModel, TMetadata> whenTrue, 
        TMetadata whenFalse)
    {
        Throw.IfNull(specification, nameof(specification));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(specification, whenTrue, _ => whenFalse);
    }
    
    public Specification(
        SpecificationBase<TModel, string> specification, 
        TMetadata whenTrue, 
        Func<TModel, TMetadata> whenFalse)
    {
        Throw.IfNull(specification, nameof(specification));
        _specification = new ChangeMetadataTypeSpecification<TModel, TMetadata, string>(specification, _ => whenTrue, whenFalse);
    }
    #endregion
    public override string Description => _specification.Description;
    public override BooleanResultBase<TMetadata> Evaluate(TModel model) => _specification.Evaluate(model);
}

public class Specification<TModel> : SpecificationBase<TModel, string>
{
    private readonly SpecificationBase<TModel, string> _specification;
    public override string Description => _specification.Description;
    public override BooleanResultBase<string> Evaluate(TModel model) => _specification.Evaluate(model);
        #region PredicateConstructors
    public Specification(
        Func<TModel, bool> predicate, 
        string whenTrue, 
        string whenFalse)
    {
        _specification = new BasicSpecification<TModel>(
            predicate,
            whenTrue,
            whenFalse);
    }
    
    public Specification(
        string description,
        Func<TModel, bool> predicate, 
        Func<TModel, string> whenTrue, 
        string whenFalse)
    {
        _specification = new BasicSpecification<TModel>(
            description,
            predicate,
            whenTrue,
            whenFalse);
    }
    
    public Specification(
        Func<TModel, bool> predicate, 
        string whenTrue, 
        Func<TModel, string> whenFalse)
    {
        _specification = new BasicSpecification<TModel>(
            predicate,
            whenTrue,
            whenFalse);
    }
    
    public Specification(
        string description,
        Func<TModel, bool> predicate, 
        Func<TModel, string> whenTrue, 
        Func<TModel, string> whenFalse)
    {
        _specification = new BasicSpecification<TModel>(
            description,
            predicate,
            whenTrue,
            whenFalse);
    }
    #endregion
    #region SpecificationConstructors
    public Specification(
        SpecificationBase<TModel, string> specification,
        string whenTrue, 
        string whenFalse)
    {
        Throw.IfNull(specification, nameof(specification));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }
    
    public Specification(
        SpecificationBase<TModel, string> specification, 
        Func<TModel, string> whenTrue, 
        string whenFalse)
    {
        Throw.IfNull(specification, nameof(specification));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }
    
    public Specification(
        SpecificationBase<TModel, string> specification, 
        string whenTrue, 
        Func<TModel, string> whenFalse)
    {
        Throw.IfNull(specification, nameof(specification));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }
    public Specification(SpecificationBase<TModel, string> specification)
    {
        Throw.IfNull(specification, nameof(specification));
        _specification = specification;
    }
    
    #endregion
    #region SpecificationFactoryConstructors
    public Specification(
        Func<SpecificationBase<TModel, string>> specificationFactory, 
        string whenTrue, 
        string whenFalse)
    {
        Throw.IfNull(specificationFactory, nameof(specificationFactory));
        var specification = specificationFactory();
        Throw.IfFactoryOutputIsNull(specification, nameof(specificationFactory));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }
    
    public Specification(
        Func<SpecificationBase<TModel, string>> specificationFactory, 
        Func<TModel, string> whenTrue, 
        string whenFalse)
    {
        Throw.IfNull(specificationFactory, nameof(specificationFactory));
        var specification = specificationFactory();
        Throw.IfFactoryOutputIsNull(specification, nameof(specificationFactory));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }
    
    public Specification(
        Func<SpecificationBase<TModel, string>> specificationFactory, 
        string whenTrue, 
        Func<TModel, string> whenFalse)
    {
        Throw.IfNull(specificationFactory, nameof(specificationFactory));
        var specification = specificationFactory();
        Throw.IfFactoryOutputIsNull(specification, nameof(specificationFactory));
        _specification = specification.SubstituteMetadata(whenTrue, whenFalse);
    }
    
    public Specification(Func<SpecificationBase<TModel, string>> specificationFactory)
    {
        Throw.IfNull(specificationFactory, nameof(specificationFactory));
        var specification = specificationFactory();
        Throw.IfFactoryOutputIsNull(specification, nameof(specificationFactory));
        _specification = specification;
    }
    #endregion
}