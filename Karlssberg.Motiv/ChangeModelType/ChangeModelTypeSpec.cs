namespace Karlssberg.Motiv.ChangeModelType;

internal sealed class ChangeModelTypeSpec<TParentModel, TModel, TMetadata>(
    SpecBase<TModel, TMetadata> spec,
    Func<TParentModel, TModel> modelSelector)
    : SpecBase<TParentModel, TMetadata>
{
    /// <inheritdoc />
    public override IProposition Proposition => spec.Proposition;

    /// <inheritdoc />
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TParentModel model)
    {
        return WrapException.IfIsSatisfiedByMethodFails(
            spec,
            () => spec.IsSatisfiedBy(modelSelector(model)));
    }

    /// <inheritdoc />
    public override string ToString() => spec.ToString();
}