namespace Karlssberg.Motiv.ChangeMetadataType;

internal sealed class MetadataToExplanationAdapterSpec<TModel, TUnderlyingModel>(
    SpecBase<TModel, TUnderlyingModel> spec) 
    : SpecBase<TModel, string>
{
    public override IProposition Proposition => spec.Proposition;

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var result = spec.IsSatisfiedBy(model);
        var metadata = result.MetadataTree switch
        {
            string reason => reason,
            _ => spec.Proposition.Statement
        };
        
        return new ChangeMetadataBooleanResult<string, TUnderlyingModel>(
            result,
            metadata,
            spec.Proposition.ToReason(result.Satisfied));
    }
}
