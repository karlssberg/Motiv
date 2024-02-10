namespace Karlssberg.Motiv.ChangeModelType;

internal sealed class ChangeModelTypeSpec<TParentModel, TModel, TMetadata>(
    SpecBase<TModel, TMetadata> spec,
    Func<TParentModel, TModel> modelSelector)
    : SpecBase<TParentModel, TMetadata>
{
    private readonly Func<TParentModel, TModel> _modelSelector = modelSelector.ThrowIfNull(nameof(modelSelector));
    private readonly SpecBase<TModel, TMetadata> _spec = spec.ThrowIfNull(nameof(spec));

    public override string Description => _spec.Description;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TParentModel model)
    {
        return WrapException.IfIsSatisfiedByInvocationFails(
            _spec,
            () => _spec.IsSatisfiedBy(_modelSelector(model)));
    }

    public override string ToString() => _spec.ToString();
}