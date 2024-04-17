namespace Karlssberg.Motiv.MetadataToExplanationAdapter;

internal sealed class MetadataToExplanationAdapterSpec<TModel, TUnderlyingModel>(
    SpecBase<TModel, TUnderlyingModel> spec) 
    : SpecBase<TModel, string>
{
    public override ISpecDescription Description => spec.Description;

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var result = spec.IsSatisfiedBy(model);

        var metadataTree = new Lazy<MetadataTree<string>>(() =>
            new MetadataTree<string>(result.Assertions));

        return new BooleanResultWithUnderlying<string, TUnderlyingModel>(
            result,
            MetadataTree,
            Explanation,
            Reason);

        MetadataTree<string> MetadataTree() => metadataTree.Value;
        Explanation Explanation() => result.Explanation;
        string Reason() => spec.Description.ToReason(result.Satisfied);
    }
}
