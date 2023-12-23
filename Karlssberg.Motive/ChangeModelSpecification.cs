using static Karlssberg.Motive.SpecificationException;

namespace Karlssberg.Motive;

public sealed class ChangeModelSpecification<TParentModel, TModel, TMetadata>(
    SpecificationBase<TModel, TMetadata> specification,
    Func<TParentModel, TModel> modelSelector) : SpecificationBase<TParentModel, TMetadata>
{
    private readonly SpecificationBase<TModel, TMetadata> _specification = Throw.IfNull(specification, nameof(specification));
    private readonly Func<TParentModel, TModel> _modelSelector = Throw.IfNull(modelSelector, nameof(modelSelector));

    public override BooleanResultBase<TMetadata> Evaluate(TParentModel model)
    {
        return WrapThrownExceptions(
            _specification,
            () => _specification.Evaluate(_modelSelector(model)));
    }

    public override string Description => _specification.Description;

    public override string ToString() => _specification.ToString();
}