namespace Motiv.ChangeModelType;

internal sealed class ChangeModelTypePolicy<TParentModel, TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> policy,
    Func<TParentModel, TModel> modelSelector)
    : PolicyBase<TParentModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => policy.Underlying;

    public override ISpecDescription Description => policy.Description;

    public override bool Matches(TParentModel model) => policy.Matches(modelSelector(model));

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TParentModel model) =>
        EvaluatePolicy(model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TParentModel model) =>
        policy.Evaluate(modelSelector(model));

    public override string ToString() => policy.ToString();
}
