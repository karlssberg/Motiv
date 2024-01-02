using static Karlssberg.Motiv.SpecificationException;

namespace Karlssberg.Motiv.ChangeModelType;

public sealed class ChangeModelTypeSpecification<TParentModel, TModel, TMetadata>(
    SpecificationBase<TModel, TMetadata> specification,
    Func<TParentModel, TModel> modelSelector) : SpecificationBase<TParentModel, TMetadata>
{
    private readonly Func<TParentModel, TModel> _modelSelector = modelSelector.ThrowIfNull(nameof(modelSelector));
    private readonly SpecificationBase<TModel, TMetadata> _specification = specification.ThrowIfNull(nameof(specification));

    public override string Description => _specification.Description;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TParentModel model)
    {
        return WrapException.IfIsSatisfiedByInvocationFails(
            _specification,
            () => _specification.IsSatisfiedBy(_modelSelector(model)));
    }

    public override string ToString() => _specification.ToString();
}