using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive.ChangeModelType;

public sealed class ChangeModelTypeSpecification<TParentModel, TModel, TMetadata>(
    SpecificationBase<TModel, TMetadata> specification,
    Func<TParentModel, TModel> modelSelector) : SpecificationBase<TParentModel, TMetadata>
{
    private readonly Func<TParentModel, TModel> _modelSelector = Throw.IfNull(modelSelector, nameof(modelSelector));
    private readonly SpecificationBase<TModel, TMetadata> _specification = Throw.IfNull(specification, nameof(specification));

    public override string Description => _specification.Description;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TParentModel model)
    {
        return WrapThrownExceptions(
            _specification,
            () => _specification.IsSatisfiedBy(_modelSelector(model)));
    }

    public override string ToString() => _specification.ToString();
}