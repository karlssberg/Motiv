namespace Motiv.ChangeModelType;

internal sealed class ChangeModelTypeSpec<TParentModel, TModel, TMetadata>(
    SpecBase<TModel, TMetadata> spec,
    Func<TParentModel, TModel> modelSelector)
    : SpecBase<TParentModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => spec.Underlying;

    public override ISpecDescription Description => spec.Description;

    protected override BooleanResultBase<TMetadata> IsSpecSatisfiedBy(TParentModel model) =>
        spec.IsSatisfiedBy(modelSelector(model));

    public override string ToString() => spec.ToString();
}
