using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive;

public class CompositeSpecification<TModel, TMetadata> : SpecificationBase<TModel, TMetadata>
{
    private readonly SpecificationBase<TModel, TMetadata> _specification;

    protected CompositeSpecification(Func<SpecificationBase<TModel, TMetadata>> specificationFactory)
        : this(specificationFactory())
    {
    }

    protected CompositeSpecification(SpecificationBase<TModel, TMetadata> specification)
    {
        _specification = Throw.IfNull(specification, nameof(specification));
    }

    public override BooleanResultBase<TMetadata> Evaluate(TModel model) =>
        WrapThrownExceptions(
            _specification, 
            () => _specification.Evaluate(model));

    public override string Description => _specification.Description;
    
    public override string ToString() => _specification.ToString();
}