namespace Karlssberg.Motiv.MetadataToExplanationAdapter;

internal sealed class MetadataToExplanationAdapterSpec<TModel, TUnderlyingModel>(
    SpecBase<TModel, TUnderlyingModel> spec) 
    : SpecBase<TModel, string>
{
    public override ISpecDescription Description => spec.Description;

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var result = spec.IsSatisfiedBy(model);
        
        return new BooleanResultWithUnderlying<string, TUnderlyingModel>(
            result,
            MetadataTree,
            Explanation,
            spec.Description.ToReason(result.Satisfied));

        MetadataTree<string> MetadataTree() => new(result.Assertions);

        Explanation Explanation() => result.Explanation;
    }
}
