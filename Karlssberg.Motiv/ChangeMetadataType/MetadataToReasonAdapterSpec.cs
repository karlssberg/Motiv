namespace Karlssberg.Motiv.ChangeMetadataType;

internal sealed class MetadataToReasonAdapterSpec<TModel, TUnderlyingModel>(
    SpecBase<TModel, TUnderlyingModel> spec) 
    : SpecBase<TModel, string>
{
    /// <inheritdoc />
    public override IProposition Proposition => spec.Proposition;

    /// <inheritdoc />
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
