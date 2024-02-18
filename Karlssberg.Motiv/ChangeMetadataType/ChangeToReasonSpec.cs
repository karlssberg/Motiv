namespace Karlssberg.Motiv.ChangeMetadataType;

internal class ChangeToReasonSpec<TModel, TUnderlyingModel>(
    SpecBase<TModel, TUnderlyingModel> spec) 
    : SpecBase<TModel, string>
{
    public override string Description => spec.Description;

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var results = spec.IsSatisfiedBy(model);
        var metadata = results.Metadata switch
        {
            string reason => reason,
            _ => Description
        };
        
        return new ChangeMetadataBooleanResult<string, TUnderlyingModel>(
            results,
            metadata,
            Description);
    }
}
