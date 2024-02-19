namespace Karlssberg.Motiv.ChangeModelType;

internal sealed class ChangeModelTypeSpec<TParentModel, TModel, TMetadata>(
    SpecBase<TModel, TMetadata> spec,
    Func<TParentModel, TModel> modelSelector)
    : SpecBase<TParentModel, TMetadata>
{
    public override string Description => spec.Description;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TParentModel model)
    {
        return WrapException.IfIsSatisfiedByMethodFails(
            spec,
            () => spec.IsSatisfiedBy(modelSelector(model)));
    }

    public override string ToString() => spec.ToString();
}