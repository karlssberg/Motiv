namespace Karlssberg.Motiv.MetadataToExplanationAdapter;

internal sealed class MetadataToExplanationAdapterSpec<TModel, TUnderlyingModel>(
    SpecBase<TModel, TUnderlyingModel> spec) 
    : SpecBase<TModel, string>
{
    public override ISpecDescription Description => spec.Description;

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var result = spec.IsSatisfiedBy(model);
        
        var newMetadata = result.MetadataTree switch
        {
            IEnumerable<string> because => because,
            _ => result.Assertions
        };
        
        return new BooleanResultWithUnderlying<string, TUnderlyingModel>(
            result,
            new MetadataTree<string>(newMetadata),
            result.Explanation,
            spec.Description.ToReason(result.Satisfied));
    }
}
